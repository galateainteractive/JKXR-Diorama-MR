# JKXR Tabletop MR — primeiro protótipo

Este branch adiciona ao **Jedi Academy** um primeiro modo de mundo em
miniatura. Este APK V1.6 abre diretamente o Jedi Academy; o código do Jedi
Outcast continua no repositório, mas não é selecionável neste pacote de teste.

## O que já está implementado

- câmera tabletop em terceira pessoa com âncora física fixa;
- escala visual inicial `0.04` (aproximadamente 1:25);
- âncora inicial a `1.25 m` à frente e `0.55 m` abaixo do headset;
- paralaxe 6DoF: mover fisicamente a cabeça muda o ponto de vista da maquete;
- IPD/estéreo convertido para a mesma escala visual;
- física, colisões, IA, missões, scripts e saves sem mudança de escala;
- um grip arrasta a maquete e dois grips arrastam, giram e mudam sua escala;
- passthrough nativo `XR_FB_passthrough`, com o fundo do céu em alpha zero;
- movimento 360 graus recalculado entre a posição atual do headset e a posição
  atual do personagem: para baixo ele vem até o observador, para cima se afasta
  e esquerda/direita permanecem laterais mesmo ao caminhar ao redor da mesa;
- o analógico usa seus eixos brutos no modo tabletop, sem herdar a orientação
  física do controle esquerdo;
- o ângulo de movimento compensa o `delta_angles` interno do Jedi Academy, para
  que spawn, scripts e transições não girem novamente a direção calculada;
- loads e reinícios de mapa invalidam explicitamente a referência anterior dos
  controles antes de reconstruí-la para a nova maquete;
- no modo tabletop, o gatilho executa o ataque primário animado do personagem,
  B liga/desliga o sabre e os gestos físicos de golpe/arremesso ficam desativados;
- aberturas e cutscenes usam a tela cinematográfica estável do JKXR, sem aplicar
  a separação estereoscópica ampliada da miniatura;
- modo reversível por cvars, sem alterar os dados originais do jogo.

O personagem agora se move dentro da âncora capturada ao carregar a fase. A
câmera só muda com o movimento físico do headset ou com um gesto de grip.

## Controles de ajuste

Os valores podem ser alterados no console do Jedi Academy:

```text
cg_tabletop 1
cg_tabletopScale 0.04
cg_tabletopForward 1.25
cg_tabletopDown 0.55
```

- `cg_tabletop 0`: volta ao JKXR normal;
- `cg_tabletopScale`: faixa segura de `0.01` a `0.20`; menor = mundo menor;
- `cg_tabletopForward`: distância física da maquete, `0.25` a `3.00 m`;
- `cg_tabletopDown`: quanto a maquete fica abaixo do headset, `0.10` a `1.50 m`.

Os limites são aplicados no código mesmo se o console receber outro valor.

## Instalação e teste

O APK não contém os arquivos comerciais de Jedi Academy. Instale o APK e siga
o procedimento abaixo usando uma cópia legal do jogo. Na instalação de PC do
Jedi Academy, localize `GameData\base` e copie `assets0.pk3`, `assets1.pk3`,
`assets2.pk3` e `assets3.pk3` para `/sdcard/JKXR/JK3/base` no Quest.
Ele mantém o package ID `com.drbeef.jkxr`, portanto atualiza/substitui uma
instalação existente do JKXR assinada com a chave pública do projeto. O nome no
menu do Quest aparece como **JKXR Tabletop V1.6** e `cg_tabletop 0` restaura o
caminho visual normal sem precisar trocar de APK.

O primeiro início solicita acesso aos arquivos e cria automaticamente
`/sdcard/JKXR/JK3/base`. Depois disso, feche o aplicativo, conecte o Quest ao PC
e faça a cópia dos quatro PK3. Não copie executáveis, DLLs nem o restante da
pasta do jogo.

Como alternativa, o instalador Windows V1.3 continua válido para copiar os
dados do jogo; depois basta atualizar este APK V1.6 por cima.
Ele procura instalações Steam/GOG, oferece busca nos discos e seleção manual,
instala o APK, copia os quatro PK3 por USB e verifica o tamanho de cada arquivo
no Quest. O ADB está incorporado; SideQuest não é obrigatório para esse fluxo.

No primeiro teste:

1. inicie Jedi Academy e carregue uma fase normal;
2. recentralize o Quest olhando para a direção onde a maquete deve ficar;
3. olhe um pouco para baixo; o personagem deve aparecer no centro da cena;
4. mova a cabeça lateralmente para verificar a paralaxe 6DoF;
5. caminhe para confirmar que o personagem se afasta dentro da maquete parada;
6. segure um grip para arrastar; segure os dois para girar e redimensionar;
7. use `cg_tabletop 0` e `cg_tabletop 1` para comparar os dois caminhos.

## Limitações conhecidas do V1.6

- mapas internos podem ter paredes ou teto entre a câmera e o jogador;
- PVS, LOD, névoa e recorte de paredes/tetos ainda precisam de tratamento;
- HUD e menus continuam usando o compositor 2D original do JKXR;
- cenas especiais, veículos e câmeras de script precisam de teste no Quest.

## Build local

Execute no PowerShell, a partir da raiz do repositório:

```powershell
.\build-tabletop.ps1
```

O script usa o JDK, Android SDK/NDK e Gradle instalados no `D:` e copia o APK
final para `D:\Codex\jkxr-tabletop-mr\artifacts\JKXR-tabletop-v1.6.apk`.
