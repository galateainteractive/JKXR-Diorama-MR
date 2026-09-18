# JKXR Tabletop MR — primeiro protótipo

Este branch adiciona ao **Jedi Academy** um primeiro modo de mundo em
miniatura. O Jedi Outcast e o caminho normal do JKXR continuam disponíveis.

## O que já está implementado

- câmera tabletop em terceira pessoa, acompanhando o jogador;
- escala visual inicial `0.04` (aproximadamente 1:25);
- âncora inicial a `1.25 m` à frente e `0.55 m` abaixo do headset;
- paralaxe 6DoF: mover fisicamente a cabeça muda o ponto de vista da maquete;
- IPD/estéreo convertido para a mesma escala visual;
- física, colisões, IA, missões, scripts e saves sem mudança de escala;
- modo reversível por cvars, sem alterar os dados originais do jogo.

Este V0 ainda não ativa passthrough. A primeira validação é propositalmente da
câmera, escala e jogabilidade. O passo seguinte será adicionar a composição
OpenXR de passthrough e tratar paredes/tetos que obstruam a visão de maquete.

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
o procedimento de dados do JKXR/SideQuest usando uma cópia legal do jogo.
Ele mantém o package ID `com.drbeef.jkxr`, portanto atualiza/substitui uma
instalação existente do JKXR assinada com a chave pública do projeto. O nome no
menu do Quest aparece como **JKXR Tabletop V0** e `cg_tabletop 0` restaura o
caminho visual normal sem precisar trocar de APK.

No primeiro teste:

1. inicie Jedi Academy e carregue uma fase normal;
2. recentralize o Quest olhando para a direção onde a maquete deve ficar;
3. olhe um pouco para baixo; o personagem deve aparecer no centro da cena;
4. mova a cabeça lateralmente para verificar a paralaxe 6DoF;
5. caminhe, lute e atravesse uma porta para conferir o acompanhamento;
6. use `cg_tabletop 0` e `cg_tabletop 1` para comparar os dois caminhos.

## Limitações conhecidas do V0

- sem passthrough/MR ainda: o fundo continua sendo o mundo do jogo;
- mapas internos podem ter paredes ou teto entre a câmera e o jogador;
- PVS, LOD, névoa, céu e recorte ainda não têm tratamento tabletop;
- a âncora segue o jogador; não há ainda gesto de agarrar/girar/escalar;
- HUD e menus continuam usando o compositor 2D original do JKXR;
- cenas especiais, veículos e câmeras de script precisam de teste no Quest.

## Build local

Execute no PowerShell, a partir da raiz do repositório:

```powershell
.\build-tabletop.ps1
```

O script usa o JDK, Android SDK/NDK e Gradle instalados no `D:` e copia o APK
final para `D:\Codex\jkxr-tabletop-mr\artifacts\JKXR-tabletop-v0.apk`.
