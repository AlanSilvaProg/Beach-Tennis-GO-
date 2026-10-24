// C# -> Poki custom events (PokiSDK.customEvent(noun, verb, data)), used to follow what the players do. The data arrives as a JSON string.
// Does nothing when the SDK is not on the page (other hosts, ad blocker): the game never depends on it.
mergeInto(LibraryManager.library, {
  PokiCustomEvent: function (noun, verb, json) {
    try {
      var n = UTF8ToString(noun), v = UTF8ToString(verb), j = UTF8ToString(json), data = {};
      try { data = j ? JSON.parse(j) : {}; } catch (e) { data = {}; }
      if (window.PokiSDK && typeof window.PokiSDK.customEvent === 'function') window.PokiSDK.customEvent(n, v, data);
    } catch (e) { }
  }
});
