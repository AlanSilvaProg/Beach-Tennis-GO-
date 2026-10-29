using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Languages: English, Portuguese (Brazil) and Spanish. The language is the first of the browser's languages (navigator.languages)
// that is pt, es or en; anything else falls back to English. "?lang=pt|es|en" in the URL forces one (to test).
// Texts: Tr("English text", args) uses the English text as the key; new texts go in the two tables below.
// Voices: see ShotAnnouncer (Assets/StreamingAssets/Voice/<code>/, only the chosen language is downloaded).
public static class I18n
{
    public enum Language { En, Pt, Es }

    public static readonly string[] Codes = { "en", "pt", "es" };

    static Language? current;

    public static Language Current
    {
        get
        {
            if (!current.HasValue) current = Detect();
            return current.Value;
        }
        set { current = value; }
    }

    public static string Code => Codes[(int)Current];

    // Static state survives a Play without domain reload: start from the detected language every time.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { current = null; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern string BrowserLanguages();
#endif

    static Language Detect()
    {
        string forced = UrlParameter("lang");
        if (!string.IsNullOrEmpty(forced) && FromTag(forced, out Language f)) return f;

        string list = null;
#if UNITY_WEBGL && !UNITY_EDITOR
        try { list = BrowserLanguages(); } catch { }
#endif
        if (!string.IsNullOrEmpty(list))
        {
            foreach (string tag in list.Split(','))
                if (FromTag(tag, out Language l)) return l;
            return Language.En;
        }
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Portuguese: return Language.Pt;
            case SystemLanguage.Spanish: return Language.Es;
            default: return Language.En;
        }
    }

    // "pt-BR" -> Pt, "es-MX" -> Es, "en-US" -> En; other languages are not supported (false).
    static bool FromTag(string tag, out Language lang)
    {
        tag = tag.Trim().ToLowerInvariant();
        int dash = tag.IndexOfAny(new[] { '-', '_' });
        if (dash > 0) tag = tag.Substring(0, dash);
        switch (tag)
        {
            case "pt": lang = Language.Pt; return true;
            case "es": lang = Language.Es; return true;
            case "en": lang = Language.En; return true;
        }
        lang = Language.En;
        return false;
    }

    public static string UrlParameter(string name)
    {
        string url = Application.absoluteURL;
        if (string.IsNullOrEmpty(url)) return null;
        int q = url.IndexOf('?');
        if (q < 0) return null;
        string query = url.Substring(q + 1);
        int hash = query.IndexOf('#');
        if (hash >= 0) query = query.Substring(0, hash);
        foreach (string pair in query.Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq > 0 && pair.Substring(0, eq) == name) return pair.Substring(eq + 1);
        }
        return null;
    }

    public static string Tr(string english, params object[] args)
    {
        string text = english;
        Language lang = Current;
        if (lang != Language.En)
        {
            var table = lang == Language.Pt ? Pt : Es;
            if (table.TryGetValue(english, out string translated)) text = translated;
        }
        return args != null && args.Length > 0 ? string.Format(text, args) : text;
    }

    static readonly Dictionary<string, string> Pt = new Dictionary<string, string>
    {
        { "PERFECT!", "PERFEITO!" },
        { "GREAT SHOT!", "QUE GOLPE!" },
        { "PERFECT SHOTS", "RAQUETADAS PERFEITAS" },
        { "GREAT SHOTS", "RAQUETADAS ÓTIMAS" },
        { "Point!", "Ponto!" },
        { "CPU point", "Ponto da CPU" },
        { "SETTINGS", "CONFIGURAÇÕES" },
        { "RACKETS", "RAQUETES" },
        { "TOURNAMENT WIN", "TORNEIO VENCIDO" },
        { "TOURNAMENT WINS {0}", "{0} TORNEIOS" },
        { "ONLINE WIN", "VITÓRIA ONLINE" },
        { "ONLINE WINS {0}", "{0} VITÓRIAS ONLINE" },
        { "PLAY AGAIN", "JOGAR DE NOVO" },
        { "Opponent point", "Ponto do adversário" },
        { "SET TO OPPONENT", "SET DO ADVERSÁRIO" },
        { "Searching for players", "Buscando jogadores" },
        { "Player found!", "Jogador encontrado!" },
        { "CANCEL", "CANCELAR" },
        { "You  vs  {0}", "Você  vs  {0}" },
        { "FREE", "GRÁTIS" },
        { "LEVEL {0}", "NÍVEL {0}" },
        { "LEVEL {0} COMPLETE!", "NÍVEL {0} COMPLETO!" },
        { "BONUS", "BÔNUS" },
        { "HIT", "BATER" },
        { "MOVE", "MOVER" },
        { "THE RACKET HITS BY ITSELF", "A RAQUETADA É AUTOMÁTICA" },
        { "CLICK", "CLIQUE" },
        { "TAP TO START", "TOQUE PARA COMEÇAR" },
        { "CLICK TO START", "CLIQUE PARA COMEÇAR" },
        { "PAUSED", "PAUSADO" },
        { "RESUME", "RETOMAR" },
        { "EXIT GAME", "SAIR DO JOGO" },
        { "MATCH REWARD", "RECOMPENSA" },
        { "RALLY COINS", "RALLY COINS" },
        { "VICTORY BONUS", "BÔNUS DE VITÓRIA" },
        { "PERFECT MATCH", "PARTIDA PERFEITA" },
        { "PERFECT SET", "SET PERFEITO" },
        { "BIG LEAD", "GRANDE VANTAGEM" },
        { "LONG MATCH", "PARTIDA LONGA" },
        { "PERFECT RALLY", "RALLY PERFEITO" },
        { "COMEBACK", "VIRADA" },
        { "NEW RACKET!", "NOVA RAQUETE!" },
        { "LV {0}", "NV {0}" },
        { "Points to win", "Pontos para vencer" },
        { "Sets to win", "Sets para vencer" },
        { "Voice", "Locução" },
        { "Music", "Música" },
        { "Sound effects", "Efeitos sonoros" },
        { "ON", "LIGADO" },
        { "OFF", "DESLIGADO" },
        { "SET TO YOU", "SET SEU" },
        { "SET TO CPU", "SET DA CPU" },
        { "Sets {0} - {1}", "Sets {0} - {1}" },
        { "CONTINUE", "CONTINUAR" },
        { "BACK", "VOLTAR" },
        { "PLAYER", "JOGADOR" },
        { "YOU", "VOCÊ" },
        { "WON", "GANHOU" },
        { "LOSE", "PERDEU" },
        { "RESTART", "REINICIAR" },
        { "RETRY", "TENTAR DE NOVO" },
        { "Retry!", "Nova tentativa!" },
        { "TRAINING MODE", "MODO TREINO" },
        { "TOURNAMENT", "TORNEIO" },
        { "ONLINE VS", "ONLINE VS" },
        { "You  vs  CPU   |   Level {0}", "Você  vs  CPU   |   Nível {0}" },
    };

    static readonly Dictionary<string, string> Es = new Dictionary<string, string>
    {
        { "PERFECT!", "¡PERFECTO!" },
        { "GREAT SHOT!", "¡QUÉ GOLPE!" },
        { "PERFECT SHOTS", "GOLPES PERFECTOS" },
        { "GREAT SHOTS", "GRANDES GOLPES" },
        { "Point!", "¡Punto!" },
        { "CPU point", "Punto de la CPU" },
        { "YOU", "¡HAS" },
        { "WON", "GANADO!" },
        { "LOSE", "PERDIDO" },
        { "SETTINGS", "AJUSTES" },
        { "RACKETS", "RAQUETAS" },
        { "TOURNAMENT WIN", "TORNEO GANADO" },
        { "TOURNAMENT WINS {0}", "{0} TORNEOS" },
        { "ONLINE WIN", "VICTORIA ONLINE" },
        { "ONLINE WINS {0}", "{0} VICTORIAS ONLINE" },
        { "PLAY AGAIN", "JUGAR DE NUEVO" },
        { "Opponent point", "Punto del rival" },
        { "SET TO OPPONENT", "SET DEL RIVAL" },
        { "Searching for players", "Buscando jugadores" },
        { "Player found!", "¡Jugador encontrado!" },
        { "CANCEL", "CANCELAR" },
        { "You  vs  {0}", "Tú  vs  {0}" },
        { "FREE", "GRATIS" },
        { "LEVEL {0}", "NIVEL {0}" },
        { "LEVEL {0} COMPLETE!", "¡NIVEL {0} COMPLETO!" },
        { "BONUS", "BONO" },
        { "HIT", "GOLPEAR" },
        { "MOVE", "MOVER" },
        { "THE RACKET HITS BY ITSELF", "EL GOLPE ES AUTOMÁTICO" },
        { "CLICK", "CLIC" },
        { "TAP TO START", "TOCA PARA EMPEZAR" },
        { "CLICK TO START", "HAZ CLIC PARA EMPEZAR" },
        { "PAUSED", "PAUSA" },
        { "RESUME", "REANUDAR" },
        { "EXIT GAME", "SALIR DEL JUEGO" },
        { "MATCH REWARD", "RECOMPENSA" },
        { "RALLY COINS", "RALLY COINS" },
        { "VICTORY BONUS", "BONO DE VICTORIA" },
        { "PERFECT MATCH", "PARTIDO PERFECTO" },
        { "PERFECT SET", "SET PERFECTO" },
        { "BIG LEAD", "GRAN VENTAJA" },
        { "LONG MATCH", "PARTIDO LARGO" },
        { "PERFECT RALLY", "RALLY PERFECTO" },
        { "COMEBACK", "REMONTADA" },
        { "NEW RACKET!", "¡NUEVA RAQUETA!" },
        { "LV {0}", "NV {0}" },
        { "Points to win", "Puntos para ganar" },
        { "Sets to win", "Sets para ganar" },
        { "Voice", "Locución" },
        { "Music", "Música" },
        { "Sound effects", "Efectos de sonido" },
        { "ON", "SÍ" },
        { "OFF", "NO" },
        { "SET TO YOU", "SET PARA TI" },
        { "SET TO CPU", "SET DE LA CPU" },
        { "Sets {0} - {1}", "Sets {0} - {1}" },
        { "CONTINUE", "CONTINUAR" },
        { "BACK", "VOLVER" },
        { "PLAYER", "JUGADOR" },
        { "RESTART", "REINICIAR" },
        { "RETRY", "REINTENTAR" },
        { "Retry!", "¡Otro intento!" },
        { "TRAINING MODE", "MODO ENTRENAMIENTO" },
        { "TOURNAMENT", "TORNEO" },
        { "ONLINE VS", "ONLINE VS" },
        { "You  vs  CPU   |   Level {0}", "Tú  vs  CPU   |   Nivel {0}" },
    };
}
