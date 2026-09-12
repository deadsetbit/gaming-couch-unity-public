mergeInto(LibraryManager.library, {
  GamingCouchInstanceStarted: function () {
    if (!window.gamingCouchInstanceStarted) {
      console.error("gamingCouchInstanceStarted is not defined");
      return;
    }
    window.gamingCouchInstanceStarted();
  },

  GamingCouchRegisterRuntimeInfo: function (runtimeInfoJsonString) {
    if (!window.gamingCouchRegisterRuntimeInfo) {
      console.error("gamingCouchRegisterRuntimeInfo is not defined");
      return;
    }

    var runtimeInfoJson = UTF8ToString(runtimeInfoJsonString);
    var runtimeInfo;
    try {
      runtimeInfo = JSON.parse(runtimeInfoJson);
    } catch (error) {
      console.error("GamingCouchRegisterRuntimeInfo received invalid JSON", error);
      return;
    }

    window.gamingCouchRegisterRuntimeInfo(runtimeInfo);
  },

  GamingCouchRegisterUnityBuildInfo: function (unityBuildInfoJsonString) {
    if (!window.gamingCouchRegisterUnityBuildInfo) {
      return;
    }

    var unityBuildInfoJson = UTF8ToString(unityBuildInfoJsonString);
    var unityBuildInfo;
    try {
      unityBuildInfo = JSON.parse(unityBuildInfoJson);
    } catch (error) {
      console.error("GamingCouchRegisterUnityBuildInfo received invalid JSON", error);
      return;
    }

    try {
      window.gamingCouchRegisterUnityBuildInfo(unityBuildInfo);
    } catch (error) {
      console.error("GamingCouchRegisterUnityBuildInfo callback failed", error);
    }
  },

  GamingCouchSetupDone: function () {
    if (!window.gamingCouchSetupDone) {
      console.error("GamingCouchSetupDone is not defined");
      return;
    }
    window.gamingCouchSetupDone();
  },

  GamingCouchSetupHud: function (hudConfigJsonString) {
    if (!window.gamingCouchSetupHud) {
      console.error("gamingCouchSetupHud is not defined");
      return;
    }

    var hudConfig;
    try {
      hudConfig = JSON.parse(UTF8ToString(hudConfigJsonString));
    } catch (error) {
      console.error("GamingCouchSetupHud received invalid JSON", error);
      return;
    }

    window.gamingCouchSetupHud(hudConfig);
  },

  GamingCouchSendProjectInfo: function (projectNameString) {
    if (!window.gamingCouchSendProjectInfo) {
      return;
    }

    var projectName = UTF8ToString(projectNameString);
    window.gamingCouchSendProjectInfo(projectName);
  },

  GamingCouchRuntimeMessages: function (runtimeMessagesJsonString) {
    if (!window.gamingCouchRuntimeMessages) {
      return;
    }

    var runtimeMessages;
    try {
      runtimeMessages = JSON.parse(UTF8ToString(runtimeMessagesJsonString));
    } catch (error) {
      console.error("GamingCouchRuntimeMessages received invalid JSON", error);
      return;
    }

    window.gamingCouchRuntimeMessages(runtimeMessages);
  },

  GamingCouchScreenSpace: function (screenSpaceJsonString) {
    if (!window.gamingCouchScreenSpace) {
      return;
    }

    var screenSpace;
    try {
      screenSpace = JSON.parse(UTF8ToString(screenSpaceJsonString));
    } catch (error) {
      console.error("GamingCouchScreenSpace received invalid JSON", error);
      return;
    }

    window.gamingCouchScreenSpace(screenSpace);
  }
});
