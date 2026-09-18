# Instalador Windows — JKXR Tabletop V0

O instalador é um executável Windows autônomo que incorpora o APK do protótipo
e as ferramentas ADB oficiais do Android SDK. Ele não incorpora arquivos do
Jedi Academy.

## Fluxo para o usuário

1. Detectar Jedi Academy na Steam, GOG ou registro do Windows;
2. opcionalmente buscar nos discos ou escolher qualquer pasta manualmente;
3. validar `assets0.pk3` a `assets3.pk3` como arquivos ZIP legíveis;
4. detectar um Quest autorizado por USB/ADB;
5. instalar o APK, criar `/sdcard/JKXR/JK3/base` e copiar os quatro PK3;
6. comparar o tamanho de cada arquivo local/remoto e abrir o jogo.

O seletor aceita a raiz do jogo, `GameData` ou diretamente `GameData/base`.

## Compilar

Com o APK já gerado em `D:\Codex\jkxr-tabletop-mr\artifacts`:

```powershell
.\installer\build-installer.ps1
```

O resultado é `D:\Codex\jkxr-tabletop-mr\artifacts\JKXR-Tabletop-V0-Installer.exe`.
O build usa o compilador .NET Framework 4.8 do Windows, portanto o executável
não exige instalação separada do .NET moderno.

## Distribuição

- APK/JKXR/OpenJK: consulte `LICENSE.txt` na raiz do repositório;
- ADB/Android SDK Platform-Tools: o `NOTICE.txt` oficial é incorporado e
  extraído ao lado do ADB no diretório temporário do instalador;
- os PK3 comerciais nunca entram no executável, Git ou pacote de distribuição;
- o executável ainda não tem assinatura Authenticode e pode acionar o aviso do
  Windows SmartScreen ao ser baixado por terceiros.
