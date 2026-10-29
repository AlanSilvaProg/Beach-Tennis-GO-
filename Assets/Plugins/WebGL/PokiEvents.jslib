// C# -> Poki custom events (PokiSDK.customEvent(noun, verb, data)), used to follow what the players do. The data arrives as a JSON string.
// Does nothing when the SDK is not on the page (other hosts, ad blocker): the game never depends on it.
mergeInto(LibraryManager.library, {
  PokiCustomEvent: function (noun, verb, json) {
    try {
      var n = UTF8ToString(noun), v = UTF8ToString(verb), j = UTF8ToString(json), data = {};
      try { data = j ? JSON.parse(j) : {}; } catch (e) { data = {}; }
      var sdk = window.PokiSDK;
      if (!sdk) return;
      if (typeof sdk.customEvent === 'function') sdk.customEvent(n, v, data);
      // Poki's documented custom event: measure(category, what, action), no "/" or "^" allowed. The action is the first text / true-false value of the data.
      if (typeof sdk.measure === 'function') {
        var clean = function (x) { return String(x).replace(/[\/^]/g, '-').substring(0, 60); }, action = 'event';
        for (var k in data) { if (typeof data[k] === 'string' || typeof data[k] === 'boolean') { action = data[k]; break; } }
        sdk.measure(clean(n), clean(v), clean(action));
      }
    } catch (e) { }
  }
});
