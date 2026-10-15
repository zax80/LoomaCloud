namespace LoomaCloud.Application.Internal;

/// <summary>
/// Recognizes a message that is only a greeting, thanks, farewell, or
/// "how are you" — optionally addressed to the assistant by name ("Hi
/// Looma", "Здравей Лума") — so it can be answered conversationally instead
/// of being sent through retrieval and web search. Without this, "Hi Looma"
/// found nothing in the indexed documents, fell through to the web-search
/// fallback, and came back as a garbled summary of an unrelated Instagram
/// brand called "hi.looma" (a real, reproduced bug).
///
/// Two layers, by design: this class is the zero-cost fast path for the most
/// widely used languages (English, Bulgarian, French, Spanish, German,
/// Italian, Portuguese, Dutch, Russian, Polish, Turkish). A hand-written
/// vocabulary can never cover every language, so anything short enough to be
/// a greeting that this vocabulary doesn't settle is classified by one tiny
/// model call in ChatCompletionUseCase, whose result is memoized (a
/// greeting is only ever classified once per process).
///
/// Strict whole-message match: every token must be a known greeting word,
/// filler, or the assistant's own name, so a real question that merely
/// starts with "Hi Looma, what is ..." is never swallowed. Pure, no I/O.
/// </summary>
internal static class SmallTalkDetector
{
    private const int MaxTokens = 6;

    private static readonly HashSet<string> AssistantNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "looma", "loomacloud", "cloud", "лума", "клауд", "лумаклауд"
    };

    /// <summary>Words that on their own make a message small talk.</summary>
    private static readonly HashSet<string> CoreWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // en
        "hi", "hello", "hey", "hiya", "greetings", "yo", "thanks", "thank", "thx", "bye", "goodbye",
        // bg
        "здравей", "здравейте", "здрасти", "здрасте", "поздрав", "благодаря", "мерси", "чао",
        "довиждане", "хей", "ехо",
        // ru
        "привет", "здравствуй", "здравствуйте", "приветствую", "спасибо", "пока", "салют",
        // fr
        "bonjour", "bonsoir", "salut", "coucou", "merci", "adieu",
        // es
        "hola", "gracias", "adiós", "adios", "chao", "saludos",
        // de
        "hallo", "danke", "tschüss", "tschuss", "servus", "moin",
        // it
        "ciao", "salve", "buongiorno", "buonasera", "buonanotte", "grazie", "arrivederci",
        // pt
        "olá", "ola", "oi", "obrigado", "obrigada", "valeu", "tchau", "adeus",
        // nl
        "hoi", "goedemorgen", "goedemiddag", "goedenavond", "goedendag", "bedankt", "dankjewel", "doei",
        // pl
        "cześć", "czesc", "hej", "dziękuję", "dziekuje", "dzięki",
        // tr
        "merhaba", "selam", "günaydın", "teşekkürler", "sağol", "görüşürüz", "nasılsın", "nasılsınız"
    };

    /// <summary>Greeting-ish multi-word expressions that have no single core word.</summary>
    private static readonly string[] SocialPhrases =
    [
        // en
        "how are you", "how is it going", "whats up", "what's up", "good morning", "good afternoon",
        "good evening", "good night",
        // bg
        "как си", "как сте", "как върви", "добро утро", "добър ден", "добър вечер", "лека нощ",
        // ru
        "доброе утро", "добрый день", "добрый вечер", "как дела", "как ты", "как поживаешь",
        "как поживаете", "до свидания", "спокойной ночи", "большое спасибо",
        // fr
        "ça va", "comment ça va", "comment allez vous", "comment vas tu", "au revoir", "bonne journée",
        "bonne soirée", "bonne nuit", "à bientôt",
        // es
        "buenos días", "buenos dias", "buenas tardes", "buenas noches", "qué tal", "que tal",
        "cómo estás", "como estas", "cómo está usted", "hasta luego", "hasta pronto", "muchas gracias",
        // de
        "guten tag", "guten morgen", "guten abend", "gute nacht", "wie geht es dir", "wie geht es ihnen",
        "wie geht's", "wie gehts", "auf wiedersehen", "vielen dank", "bis bald",
        // it
        "buon giorno", "buona sera", "buona notte", "come stai", "come va", "come sta", "a presto",
        "mille grazie",
        // pt
        "bom dia", "boa tarde", "boa noite", "tudo bem", "como vai", "como está", "muito obrigado",
        "muito obrigada", "até logo",
        // nl
        "hoe gaat het", "hoe is het", "tot ziens", "dank u", "dank je", "tot later",
        // pl
        "dzień dobry", "dobry wieczór", "jak się masz", "do widzenia", "dobranoc",
        // tr
        "teşekkür ederim", "iyi günler", "iyi akşamlar", "iyi geceler", "hoşça kal", "çok teşekkürler"
    ];

    /// <summary>Connective words allowed around the core words/phrases.</summary>
    private static readonly HashSet<string> FillerWords = BuildFillerWords();

    private static HashSet<string> BuildFillerWords()
    {
        var fillers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "there", "dear", "you", "so", "much", "a", "lot", "all", "again", "friend",
            "ти", "ви", "пак", "отново", "много",
            "beaucoup", "mucho", "molto", "muito", "sehr", "veel", "bardzo", "çok", "dziękuję",
            "très", "bien", "tout", "le", "monde", "todos", "alle", "tutti", "todo", "mundo",
            "everyone", "everybody", "всички"
        };

        // Every token of every social phrase is itself an allowed filler, so
        // a phrase like "как си" can match as a whole message.
        foreach (var phrase in SocialPhrases)
        {
            foreach (var token in Tokenize(phrase))
            {
                fillers.Add(token);
            }
        }

        return fillers;
    }

    /// <summary>
    /// Language-agnostic precheck: could this message plausibly be small
    /// talk, in ANY language? Anything short enough that the vocabulary
    /// above didn't settle is handed to a tiny model classification call
    /// (see ChatCompletionUseCase). Length only — no language assumptions
    /// — so it works for scripts without spaces too.
    /// </summary>
    public static bool MightBeSmallTalk(string message)
    {
        var trimmed = message.Trim();
        return trimmed.Length is > 0 and <= 40 && Tokenize(trimmed).Count <= 8;
    }

    /// <summary>Normalized, case-insensitive cache key for memoizing classifications.</summary>
    public static string NormalizeKey(string message) => string.Join(' ', Tokenize(message)).ToLowerInvariant();

    public static bool IsSmallTalk(string message)
    {
        var tokens = Tokenize(message);
        if (tokens.Count == 0 || tokens.Count > MaxTokens)
        {
            return false;
        }

        var hasCore = false;
        foreach (var token in tokens)
        {
            if (AssistantNames.Contains(token) || CoreWords.Contains(token))
            {
                hasCore = true;
            }
            else if (!FillerWords.Contains(token))
            {
                return false;
            }
        }

        if (hasCore)
        {
            return true;
        }

        var joined = string.Join(' ', tokens);
        return SocialPhrases.Any(phrase => joined.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> Tokenize(string message)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var ch in message)
        {
            if (char.IsLetter(ch) || ch == '\'' || ch == '’')
            {
                current.Append(ch == '’' ? '\'' : ch);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
