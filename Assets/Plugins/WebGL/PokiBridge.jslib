// C# -> page JavaScript for the Poki SDK. The logic (event de-duplication, focus, ads) lives in window.PokiBridge, defined in the
// WebGL template (Assets/WebGLTemplates/BeachTennis/index.html). Without that template these calls do nothing.
mergeInto(LibraryManager.library, {
  PokiGameplayStart: function () {
    if (window.PokiBridge) window.PokiBridge.gameplayStart();
  },
  PokiGameplayStop: function () {
    if (window.PokiBridge) window.PokiBridge.gameplayStop();
  },
  // Rewarded ad: the game waits for "OnRewardedFinished" ("1" = watched to the end, "0" = no reward). Returns 0 when nothing will report back.
  PokiRewardedBreak: function () {
    if (!window.PokiBridge) return 0;
    window.PokiBridge.rewardedBreak();
    return 1;
  },
  // Returns 1 when the page started the break (the game waits for the "OnBreakFinished" message), 0 when there is nothing to wait for.
  PokiCommercialBreak: function () {
    if (!window.PokiBridge) return 0;
    window.PokiBridge.commercialBreak();
    return 1;
  }
});
