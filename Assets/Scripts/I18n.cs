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

    static string UrlParameter(string name)
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
        { "BEACH TENNIS GO!\nClick / tap to play", "BEACH TENNIS GO!\nClique / toque para jogar" },
        { "Your serve\nSwing the racket at the ball", "Seu saque\nBata com a raquete na bola" },
        { "CPU serve", "Saque da CPU" },
        { "(pull the racket back first)", "(puxe a raquete para trás primeiro)" },
        { "Point!", "Ponto!" },
        { "CPU point", "Ponto da CPU" },
        { "YOU WIN!\nClick / tap for next level", "VOCÊ VENCEU!\nClique / toque para o próximo nível" },
        { "YOU LOSE\nClick / tap to retry", "VOCÊ PERDEU\nClique / toque para tentar de novo" },
        { "You  vs  CPU   |   Level {0}", "Você  vs  CPU   |   Nível {0}" },
    };

    static readonly Dictionary<string, string> Es = new Dictionary<string, string>
    {
        { "BEACH TENNIS GO!\nClick / tap to play", "BEACH TENNIS GO!\nHaz clic / toca para jugar" },
        { "Your serve\nSwing the racket at the ball", "Tu saque\nGolpea la pelota con la raqueta" },
        { "CPU serve", "Saque de la CPU" },
        { "(pull the racket back first)", "(echa la raqueta hacia atrás primero)" },
        { "Point!", "¡Punto!" },
        { "CPU point", "Punto de la CPU" },
        { "YOU WIN!\nClick / tap for next level", "¡GANASTE!\nHaz clic / toca para el siguiente nivel" },
        { "YOU LOSE\nClick / tap to retry", "PERDISTE\nHaz clic / toca para reintentar" },
        { "You  vs  CPU   |   Level {0}", "Tú  vs  CPU   |   Nivel {0}" },
    };
}
