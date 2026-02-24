using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public class AutoRunImportMeshySoldier
{
    static AutoRunImportMeshySoldier()
    {
        EditorApplication.delayCall += RunSetupOnce;
    }

    static void RunSetupOnce()
    {
        if (SessionState.GetBool("MeshySetupDone", false)) return;
        SessionState.SetBool("MeshySetupDone", true);

        ImportMeshySoldier.Setup();
        
        // Write a log file we can read from the agent
        File.WriteAllText("MeshySetupLog.txt", "Setup executed successfully.");
    }
}
