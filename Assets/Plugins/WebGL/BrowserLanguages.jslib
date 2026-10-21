// Languages of the browser in order of preference (navigator.languages), comma separated, e.g. "pt-BR,pt,en-US,en".
mergeInto(LibraryManager.library, {
  BrowserLanguages: function () {
    var s = (navigator.languages && navigator.languages.length) ? navigator.languages.join(",") : (navigator.language || "en");
    var size = lengthBytesUTF8(s) + 1;
    var buffer = _malloc(size);
    stringToUTF8(s, buffer, size);
    return buffer;
  }
});
