using UnityEditor;

internal static class GamingCouchMenuPriorities
{
  internal const int StartScreen = 0;
  internal const int CreateExampleScene = 1;
  internal const int WebGLBuild = 20;
  internal const int CreateGamingCouchGameObject = 21;
  internal const int Documentation = 40;
}

public class GamingCouchMenuItems
{
  [MenuItem("GamingCouch/Documentation", false, GamingCouchMenuPriorities.Documentation)]
  static void OpenDocumentation()
  {
    GamingCouchDocumentation.Open();
  }

  [MenuItem("GamingCouch/Start Screen", false, GamingCouchMenuPriorities.StartScreen)]
  static void OpenStartScreen()
  {
    GamingCouchStartScreenWindow.Open();
  }

  [MenuItem("GamingCouch/Create Example Scene/Template", false, GamingCouchMenuPriorities.CreateExampleScene)]
  static void CreateTemplateScene()
  {
    CreateScene(GCExampleSceneKind.Template);
  }

  [MenuItem("GamingCouch/Create Example Scene/Example Game", false, GamingCouchMenuPriorities.CreateExampleScene)]
  static void CreateExampleGameScene()
  {
    CreateScene(GCExampleSceneKind.ExampleGame);
  }

  static void CreateScene(GCExampleSceneKind kind)
  {
    var result = GamingCouchExampleSceneCreation.CreateScene(kind);
    if (result.IsCancelled)
    {
      return;
    }

    var window = GamingCouchStartScreenWindow.Open();
    window.ApplyExternalSetupActionResult(
      GamingCouchStartScreenSetupActions.FromExampleSceneCreationResult(result)
    );
  }

  [MenuItem("GameObject/GamingCouch", false, 0)]
  [MenuItem("GamingCouch/Create GamingCouch GameObject", false, GamingCouchMenuPriorities.CreateGamingCouchGameObject)]
  static void CreatePrefabInstance()
  {
    var result = GamingCouchSceneWiring.EnsureActiveSceneGamingCouch();
    if (result.IsBlocked)
    {
      EditorUtility.DisplayDialog("Create GamingCouch", result.message, "OK");
      return;
    }

    if (result.gamingCouch != null)
    {
      Selection.activeObject = result.gamingCouch.gameObject;
    }
  }
}
