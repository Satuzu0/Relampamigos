using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu "Infiltrate" para armar la escena principal con un solo click XD
public static class ConstructorEscena
{
    const string RutaEscena = "Assets/Scenes/MainUI.unity";

    [MenuItem("Infiltrate/1. Armar escena principal")]
    public static void Armar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // camara negra, solo para que Unity no se queje de que no hay camara
        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;

        var db = new GameObject("DatabaseManager");
        db.AddComponent<DatabaseManager>();

        var ui = new GameObject("UIManager", typeof(RectTransform));
        ui.layer = LayerMask.NameToLayer("UI");
        ui.AddComponent<Canvas>();
        ui.AddComponent<UIManager>();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(escena, RutaEscena);

        // la dejamos como unica escena del build
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RutaEscena, true) };

        Selection.activeGameObject = db;
        Debug.Log("Escena lista en " + RutaEscena + ". Dale Play y listo cat.");
    }

    [MenuItem("Infiltrate/2. Comprobar instalación")]
    public static void Comprobar()
    {
        bool mysql = Type.GetType("MySqlConnector.MySqlConnection, MySqlConnector") != null;
        bool input = false;
#if ENABLE_INPUT_SYSTEM
        input = true;
#endif
        string msg =
            "MySqlConnector: " + (mysql ? "OK" : "NO ESTÁ (instálalo desde NuGet)") + "\n" +
            "Input System nuevo: " + (input ? "activo" : "no (usa el viejo, también funciona)") + "\n" +
            "Escena: " + (File.Exists(RutaEscena) ? "existe" : "falta, usa Infiltrate > 1. Armar escena principal");
        Debug.Log(msg);
        EditorUtility.DisplayDialog("Comprobación", msg, "Listo");
    }
}
