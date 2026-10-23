# Poki SDK — Beach Tennis GO!

- **Game ID:** `761a602c-88ba-435a-8df8-d5fdbe420364` (`PokiService.GameId`). Nenhuma chamada usada aqui pede o id; ele serve para serviços por jogo da Poki (AUDS), se forem adicionados.
- **Teste oficial:** suba a build no Poki Inspector (https://inspector.poki.dev/) e depois peça a revisão. Guia: https://developers.poki.com/guide/sdk-unity

## Como a integração funciona (importante)
Na poki.com **o `index.html` do jogo NÃO é usado**: a página é gerada pelo loader da Poki a partir do `index.json` do template. Por isso o jogo usa a **classe e o plugin oficiais da Poki** (os mesmos que o loader deles espera):
- `Assets/Scripts/PokiUnitySDK.cs` e `Assets/Plugins/WebGL/PokiSDKBridge.jslib` — arquivos oficiais do pacote Unity da Poki (https://game-cdn.poki.com/wrappers/v3/unity2022.zip), **sem alterações** (os avisos de "obsolete" do compilador vêm deles).
- `Assets/WebGLTemplates/BeachTennis/index.json` — o que a Poki lê (nomes dos arquivos da build com hash, etc.). Gerado a partir de `scripts/webgl-template/index.json` por `scripts/build-webgl-template.py`.
- `Assets/WebGLTemplates/BeachTennis/index.html` — nossa página (tela de carregamento com logo/barra), usada em **localhost e GitHub Pages**. Ela implementa a mesma API de página que o loader da Poki (`initPokiBridge`, `commercialBreak`, `rewardedBreak`...), então o código do jogo é idêntico em todo lugar. Sem o script do SDK (bloqueado/offline) entra um substituto que nunca lança erro.
- `Assets/Scripts/PokiService.cs` — o lado do jogo, em cima da classe oficial.

## Fluxo
| Momento | Chamada |
|---|---|
| Início (Awake) | `gameLoadingStart` + `init` |
| Primeiro quadro | `gameLoadingFinished` |
| Toque em jogar / tela de fim → próxima partida / RESTART | `commercialBreak` e depois `StartMatch` → `gameplayStart` |
| Partida acaba (vitória ou derrota) | `gameplayStop` |
| Janela/aba sem foco | `gameplayStop`; ao voltar, `gameplayStart` só se a partida segue em andamento |
| RETRY (tela de derrota) | `rewardedBreak`; só com recompensa concedida joga o mesmo nível de novo |

Garantias:
- `gameplayStart`/`gameplayStop` nunca se repetem em sequência (estado derivado de "o jogo quer jogar" E "página visível" E "sem anúncio"; só envia quando o resultado muda).
- Nenhum evento do SDK durante o anúncio. O jogo fica mudo (`AudioListener.volume = 0`) e ignora input (`PokiService.AdPlaying`). `gameplayStop` sai **antes** do anúncio.
- `commercialBreak` antes de **todo** `gameplayStart` em que o jogador quer continuar (a Poki decide se há anúncio); falha de anúncio/SDK é silenciosa, sem timer interno de anúncios. Teto de 90 s para um anúncio que não termina.
- Rewarded: botão com ícone de vídeo, nunca verde, o botão padrão (RESTART) é maior e fica acima; sem anúncio/bloqueador = sem recompensa e sem mensagem; não paga duas vezes.
- No Editor a classe oficial só registra no console: o intervalo termina na hora e a recompensa é concedida (para testar).
- Anúncios só dentro da Poki (iframe/host "poki"); em hosts locais o SDK roda em debug com anúncios simulados; em outros hosts (ex.: GitHub Pages) o intervalo termina na hora e o Retry não dá recompensa.
- Página: espaço, setas ↑↓ e roda do mouse não rolam a página.

## Configurações de build (guia de otimização da Poki)
Aplicadas por `BuildScript.ApplyPokiBuildSettings()` antes de **todo** build:
- Compressão **Disabled** (os servidores da Poki comprimem; por isso `.data` e `.wasm` saem "crus", maiores no zip) e `decompressionFallback` desligado.
- **Name Files As Hashes** ligado (evita cache velho).
- Exceções: None. Strip Engine Code ligado. Managed Stripping **High**. Data Caching **desligado** de propósito: no inspector da Poki, recarregar a página travava na tela de loading (só abrindo a aba de novo funcionava) com o cache do Unity (`[UnityCache]` / IndexedDB) ligado; com nomes de arquivo em hash o cache HTTP do navegador já cobre o reuso. API Compatibility .NET Standard 2.0.
- Já feitos antes: áudio mono (Vorbis), texturas com crunch, sprites sem mipmaps, módulos mínimos, vozes só do idioma do jogador (StreamingAssets).

## Checklist dos requisitos (QA da Poki)
- Desktop/mobile/tablet: mouse e toque; tablet usa o controle por toque (`Application.isMobilePlatform`).
- 16:9: o canvas cobre a janela toda e a câmera se adapta ao formato da tela.
- Incognito: o jogo não usa localStorage/PlayerPrefs; o cache de dados da Unity (IndexedDB) falha sem derrubar o jogo.
- Sem requisições externas: fontes, sons e vozes estão na build; só o script do SDK vem da CDN da Poki.
- Sem branding/anúncios externos, sem links externos (não precisa de política de privacidade in-game).
- Idiomas: inglês, português e espanhol (detecção pelo navegador).
- **Pendentes (fora do código):** thumbnails estática e animada (com a Poki, via Discord) e as imagens de `screenshots` do template (até 4, `1.jpg` + `1-small.jpg`...) para o loader da Poki; teste no Poki Inspector; save de progresso (não há progresso persistente: o jogo é partida a partida).
