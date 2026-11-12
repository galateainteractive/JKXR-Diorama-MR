/*
===========================================================================
Copyright (C) 2005 - 2015, ioquake3 contributors
Copyright (C) 2013 - 2015, OpenJK contributors

This file is part of the OpenJK source code.

OpenJK is free software; you can redistribute it and/or modify it
under the terms of the GNU General Public License version 2 as
published by the Free Software Foundation.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, see <http://www.gnu.org/licenses/>.
===========================================================================
*/

#include <unistd.h>
#include <fcntl.h>
#include <stdlib.h>
#include <sys/types.h>
#include <sys/ioctl.h>
#include <sys/mman.h>
//#include <sys/shm.h>
#include <sys/wait.h>
#ifdef __linux__ // rb0101023 - guard this
#include <linux/soundcard.h>
#endif
#ifdef __FreeBSD__ // rb0101023 - added
#include <sys/soundcard.h>
#endif
#include <stdio.h>

#include "qcommon/q_shared.h"
#include "client/snd_local.h"

//Updated by Emile Belanger for OpenSL

// for native audio
#include <SLES/OpenSLES.h>
#include <SLES/OpenSLES_Android.h>
#include <pthread.h>
#include <atomic>

pthread_mutex_t dma_mutex = PTHREAD_MUTEX_INITIALIZER;

// engine interfaces
static SLObjectItf engineObject = NULL;
static SLEngineItf engineEngine;

// output mix interfaces
static SLObjectItf outputMixObject = NULL;

// buffer queue player interfaces
static SLObjectItf bqPlayerObject = NULL;
static SLPlayItf bqPlayerPlay;
static SLAndroidSimpleBufferQueueItf bqPlayerBufferQueue;

static SLEffectSendItf bqPlayerEffectSend;
static SLMuteSoloItf bqPlayerMuteSolo;
static SLVolumeItf bqPlayerVolume;

void myassert(int v,const char * message)
{
	if (!v)
		Com_Printf("myassert: %s",message);
}

int audio_fd;
int snd_inited = 0;

cvar_t *sndbits;
cvar_t *sndspeed;
cvar_t *sndchannels;

cvar_t *snddevice;

static int dmapos = 0;
static int dmasize = 0;

#define OPENSL_BUFF_LEN 1024
#define OPENSL_SAMPLE_RATE_HZ 48000
#define AUDIO_RESUME_STABLE_MS 250
#define AUDIO_STALL_TIMEOUT_MS 1500
#define AUDIO_RECOVERY_COOLDOWN_MS 2000

static unsigned char play_buffer[OPENSL_BUFF_LEN];

static std::atomic<bool> audio_output_ready(false);
static std::atomic<unsigned int> audio_callback_serial(0);
static std::atomic<bool> audio_output_fault(false);
static qboolean audio_app_active = qtrue;
static int audio_resume_not_before = 0;
static int audio_last_progress_time = 0;
static int audio_last_recovery_time = -AUDIO_RECOVERY_COOLDOWN_MS;
static unsigned int audio_observed_callback_serial = 0;

static qboolean OpenSL_CreateOutput();
static void OpenSL_DestroyOutput();

static qboolean OpenSL_CheckResult(SLresult result, const char *operation)
{
	if (result == SL_RESULT_SUCCESS)
	{
		return qtrue;
	}

	Com_Printf("OpenSL: %s failed (result=%u)\n", operation, (unsigned int)result);
	return qfalse;
}

void bqPause(int p)
{
	SLresult result;
	if (!bqPlayerPlay || !audio_output_ready.load(std::memory_order_acquire))
	{
		return;
	}

	if (p)
	{
		result = (*bqPlayerPlay)->SetPlayState(bqPlayerPlay, SL_PLAYSTATE_PAUSED);
		myassert(SL_RESULT_SUCCESS == result,"SetPlayState");
	}
	else
	{
		result = (*bqPlayerPlay)->SetPlayState(bqPlayerPlay, SL_PLAYSTATE_PLAYING);
		myassert(SL_RESULT_SUCCESS == result,"SetPlayState");

		result = (*bqPlayerBufferQueue)->Enqueue(bqPlayerBufferQueue, "\0", 1);
		myassert(SL_RESULT_SUCCESS == result,"Enqueue first buffer");
	}
}

//NOTE!! There are definetly threading issues with this, but it appears to work for now...

// TEST is me testing black screen issue!

void bqPlayerCallback(SLAndroidSimpleBufferQueueItf bq, void *context)
{
	if (!audio_output_ready.load(std::memory_order_acquire))
	{
		return;
	}

	pthread_mutex_lock(&dma_mutex);
	if (!snd_inited || !audio_output_ready.load(std::memory_order_relaxed) || !bqPlayerBufferQueue)
	{
		pthread_mutex_unlock(&dma_mutex);
		return;
	}

	int pos = (dmapos * (dma.samplebits/8));
	if (pos >= dmasize)
		dmapos = pos = 0;

	int len = OPENSL_BUFF_LEN;
	int factor = 2*2;
	int FrameCount = (unsigned int)OPENSL_BUFF_LEN / factor;

	if (!snd_inited)  /* shouldn't happen, but just in case... */
	{
		memset(play_buffer, '\0', len);
		pthread_mutex_unlock(&dma_mutex);
		return;
	}
	else
	{
		int tobufend = dmasize - pos;  /* bytes to buffer's end. */
		int len1 = len;
		int len2 = 0;

		if (len1 > tobufend)
		{
			len1 = tobufend;
			len2 = len - len1;
		}
		memcpy(play_buffer, dma.buffer + pos, len1);
		if (len2 <= 0)
			dmapos += (len1 / (dma.samplebits/8));
		else  /* wraparound? */
		{
			memcpy(play_buffer+len1, dma.buffer, len2);
			dmapos = (len2 / (dma.samplebits/8));
		}
	}

	if (dmapos >= dmasize)
		dmapos = 0;

	SLresult result;
	//LOGI("Frame count = %d",FrameCount);
	if (FrameCount == 0)
		FrameCount = 1;
	result = (*bqPlayerBufferQueue)->Enqueue(bqPlayerBufferQueue, play_buffer,FrameCount * factor);
	if (result == SL_RESULT_SUCCESS)
	{
		audio_callback_serial.fetch_add(1, std::memory_order_relaxed);
	}
	else
	{
		audio_output_fault.store(true, std::memory_order_release);
	}

	pthread_mutex_unlock(&dma_mutex);
}

static void OpenSL_DestroyOutput()
{
	audio_output_ready.store(false, std::memory_order_release);

	// Wait for a callback already in flight before destroying its interfaces.
	pthread_mutex_lock(&dma_mutex);
	pthread_mutex_unlock(&dma_mutex);

	if (bqPlayerPlay)
	{
		(*bqPlayerPlay)->SetPlayState(bqPlayerPlay, SL_PLAYSTATE_PAUSED);
	}
	if (bqPlayerObject)
	{
		(*bqPlayerObject)->Destroy(bqPlayerObject);
	}
	if (outputMixObject)
	{
		(*outputMixObject)->Destroy(outputMixObject);
	}
	if (engineObject)
	{
		(*engineObject)->Destroy(engineObject);
	}

	bqPlayerObject = NULL;
	bqPlayerPlay = NULL;
	bqPlayerBufferQueue = NULL;
	outputMixObject = NULL;
	engineEngine = NULL;
	engineObject = NULL;
}

static qboolean OpenSL_CreateOutput()
{
	SLresult result;

	OpenSL_DestroyOutput();

	result = slCreateEngine(&engineObject, 0, NULL, 0, NULL, NULL);
	if (!OpenSL_CheckResult(result, "slCreateEngine"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	result = (*engineObject)->Realize(engineObject, SL_BOOLEAN_FALSE);
	if (!OpenSL_CheckResult(result, "Realize engine"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	result = (*engineObject)->GetInterface(engineObject, SL_IID_ENGINE, &engineEngine);
	if (!OpenSL_CheckResult(result, "GetInterface engine"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	result = (*engineEngine)->CreateOutputMix(engineEngine, &outputMixObject, 0, NULL, NULL);
	if (!OpenSL_CheckResult(result, "CreateOutputMix"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	result = (*outputMixObject)->Realize(outputMixObject, SL_BOOLEAN_FALSE);
	if (!OpenSL_CheckResult(result, "Realize output mix"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	SLDataLocator_AndroidSimpleBufferQueue loc_bufq = {
		SL_DATALOCATOR_ANDROIDSIMPLEBUFFERQUEUE, 1
	};
	SLDataFormat_PCM format_pcm = {
		SL_DATAFORMAT_PCM,
		2,
		SL_SAMPLINGRATE_48,
		SL_PCMSAMPLEFORMAT_FIXED_16,
		SL_PCMSAMPLEFORMAT_FIXED_16,
		SL_SPEAKER_FRONT_LEFT | SL_SPEAKER_FRONT_RIGHT,
		SL_BYTEORDER_LITTLEENDIAN
	};
	SLDataSource audioSrc = {&loc_bufq, &format_pcm};
	SLDataLocator_OutputMix loc_outmix = {SL_DATALOCATOR_OUTPUTMIX, outputMixObject};
	SLDataSink audioSnk = {&loc_outmix, NULL};
	const SLInterfaceID ids[1] = {SL_IID_ANDROIDSIMPLEBUFFERQUEUE};
	const SLboolean req[1] = {SL_BOOLEAN_TRUE};

	result = (*engineEngine)->CreateAudioPlayer(
		engineEngine, &bqPlayerObject, &audioSrc, &audioSnk, 1, ids, req);
	if (!OpenSL_CheckResult(result, "CreateAudioPlayer"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	result = (*bqPlayerObject)->Realize(bqPlayerObject, SL_BOOLEAN_FALSE);
	if (!OpenSL_CheckResult(result, "Realize audio player"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	result = (*bqPlayerObject)->GetInterface(bqPlayerObject, SL_IID_PLAY, &bqPlayerPlay);
	if (!OpenSL_CheckResult(result, "GetInterface play"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	result = (*bqPlayerObject)->GetInterface(
		bqPlayerObject, SL_IID_BUFFERQUEUE, &bqPlayerBufferQueue);
	if (!OpenSL_CheckResult(result, "GetInterface buffer queue"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	result = (*bqPlayerBufferQueue)->RegisterCallback(
		bqPlayerBufferQueue, bqPlayerCallback, NULL);
	if (!OpenSL_CheckResult(result, "RegisterCallback"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	result = (*bqPlayerPlay)->SetPlayState(bqPlayerPlay, SL_PLAYSTATE_PLAYING);
	if (!OpenSL_CheckResult(result, "SetPlayState playing"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	pthread_mutex_lock(&dma_mutex);
	audio_output_fault.store(false, std::memory_order_relaxed);
	audio_output_ready.store(true, std::memory_order_release);
	result = (*bqPlayerBufferQueue)->Enqueue(bqPlayerBufferQueue, "\0", 1);
	pthread_mutex_unlock(&dma_mutex);

	if (!OpenSL_CheckResult(result, "Enqueue first buffer"))
	{
		OpenSL_DestroyOutput();
		return qfalse;
	}

	audio_observed_callback_serial = audio_callback_serial.load(std::memory_order_relaxed);
	audio_last_progress_time = Sys_Milliseconds();
	Com_Printf("OpenSL: output active at %d Hz\n", OPENSL_SAMPLE_RATE_HZ);
	return qtrue;
}

static void OpenSL_RecoverOutput(const char *reason)
{
	const int now = Sys_Milliseconds();
	if ((now - audio_last_recovery_time) < AUDIO_RECOVERY_COOLDOWN_MS)
	{
		return;
	}

	audio_last_recovery_time = now;
	Com_Printf("OpenSL: rebuilding audio output (%s)\n", reason);
	OpenSL_DestroyOutput();

	if (audio_app_active && OpenSL_CreateOutput())
	{
		audio_last_progress_time = now;
	}
}

int SNDDMA_Init( int sampleFrequencyInKHz ) {
	if ( snd_inited ) {
		return 1;
	}

	dmapos = 0;
	dma.samplebits = 16;
	dma.channels = 2;
	dma.samples = 1024*16;
	dma.submission_chunk = 1024*2;
	dma.speed = OPENSL_SAMPLE_RATE_HZ;
	dmasize = (dma.samples * (dma.samplebits/8));
	dma.buffer = (byte*)calloc(1, dmasize);
	if (!dma.buffer)
	{
		Com_Printf("OpenSL: failed to allocate DMA buffer\n");
		return 0;
	}

	snd_inited = 1;
	audio_callback_serial.store(0, std::memory_order_relaxed);
	audio_observed_callback_serial = 0;
	audio_last_progress_time = Sys_Milliseconds();
	Com_Printf("OpenSL: requested s_khz=%d, using native Quest output at %d Hz\n",
		sampleFrequencyInKHz, OPENSL_SAMPLE_RATE_HZ);

	if (audio_app_active && !OpenSL_CreateOutput())
	{
		snd_inited = 0;
		free(dma.buffer);
		dma.buffer = NULL;
		dmasize = 0;
		return 0;
	}

	return 1;
}

int SNDDMA_GetDMAPos( void ) {
	struct count_info count;

	if ( !snd_inited ) {
		return 0;
	}

	// TEST pthread_mutex_lock(&dma_mutex);

	//LOGI("SNDDMA_GetDMAPos");
	return dmapos;
}

void SNDDMA_Shutdown( void ) {
	snd_inited = 0;
	OpenSL_DestroyOutput();
	if (dma.buffer)
	{
		free(dma.buffer);
		dma.buffer = NULL;
	}
	dmasize = 0;
	dmapos = 0;
}

void SNDDMA_SetAppState(qboolean active)
{
	const int now = Sys_Milliseconds();
	active = active ? qtrue : qfalse;

	if (audio_app_active != active)
	{
		audio_app_active = active;
		if (!active)
		{
			if (audio_output_ready.load(std::memory_order_acquire))
			{
				Com_Printf("OpenSL: pausing and releasing audio output\n");
				OpenSL_DestroyOutput();
			}
			return;
		}

		audio_resume_not_before = now + AUDIO_RESUME_STABLE_MS;
		audio_last_progress_time = now;
		Com_Printf("OpenSL: Activity/OpenXR active; waiting for stable focus\n");
	}

	if (!active || !snd_inited)
	{
		return;
	}

	if (!audio_output_ready.load(std::memory_order_acquire))
	{
		if ((now - audio_resume_not_before) >= 0 &&
			(now - audio_last_recovery_time) >= AUDIO_RECOVERY_COOLDOWN_MS)
		{
			audio_last_recovery_time = now;
			Com_Printf("OpenSL: reopening audio output after lifecycle transition\n");
			OpenSL_CreateOutput();
		}
		return;
	}

	const unsigned int callbackSerial = audio_callback_serial.load(std::memory_order_relaxed);
	if (callbackSerial != audio_observed_callback_serial)
	{
		audio_observed_callback_serial = callbackSerial;
		audio_last_progress_time = now;
	}

	if (audio_output_fault.exchange(false, std::memory_order_acq_rel))
	{
		OpenSL_RecoverOutput("buffer queue error");
	}
	else if ((now - audio_last_progress_time) >= AUDIO_STALL_TIMEOUT_MS)
	{
		OpenSL_RecoverOutput("audio callback stalled");
	}
}

/*
==============
SNDDMA_Submit

Send sound to device if buffer isn't really the dma buffer
===============
 */
void SNDDMA_Submit( void ) {
	//LOGI("SNDDMA_Submit");
	// TEST pthread_mutex_unlock(&dma_mutex);
}

void SNDDMA_BeginPainting( void ) {
	//LOGI("SNDDMA_BeginPainting");
	//pthread_mutex_lock(&dma_mutex);
}
