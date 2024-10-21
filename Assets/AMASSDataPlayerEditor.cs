using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

[CustomEditor(typeof(AMASSDataPlayer))]

public class AMASSDataPlayerEditor : Editor
{
    SerializedProperty fileName;
    SerializedProperty frameNo;
    bool showDefault = false;
    List<string> _files;
    string adddirectory;
    void OnEnable()
    {
        _files = new List<string>();
        fileName = serializedObject.FindProperty("_filename");
        frameNo = serializedObject.FindProperty("_frame");
    }
    Vector2 scroll;
    public override void OnInspectorGUI()
    {
        if (showDefault = GUILayout.Toggle(showDefault, "Show Default Inspector"))
        {
            DrawDefaultInspector();
        }

        AMASSDataPlayer myScript = (AMASSDataPlayer)target;

        GUILayout.Label("Point to a directory to add files");
        adddirectory = EditorGUILayout.TextField(adddirectory);

        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add all files in dir to list"))
        {
            string[] filesindir = FetchFilesInDir(adddirectory);
            foreach (var file in filesindir)
            {
                var normalizedDir = new FileInfo(file).FullName;
                if (!_files.Contains(normalizedDir))
                    _files.Add(normalizedDir);
            }
        }
        if (GUILayout.Button("purge list"))
            _files = new List<string> { };
        GUILayout.EndHorizontal();
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(300));
        foreach (var file in _files)
        {
            var abbrbName = file.Replace("_geo.npz", "");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("¢º", GUILayout.Width(20)))
            {
                myScript.LoadAnimation(file);
            }
            GUILayout.Label(abbrbName);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
            


        serializedObject.Update();
        EditorGUILayout.PropertyField(fileName, new GUIContent("current file"), GUILayout.ExpandWidth(true));

        if (myScript.Ready == false)
            return;
        if (myScript.Ready)
        {
            EditorGUILayout.Slider(frameNo, 0, myScript.FrameCount);
            if (GUILayout.Button("Play"))
            {
                myScript.Play();
            }
            if (GUILayout.Button("Pause"))
            {
                myScript.Pause();
            }
        }

        serializedObject.ApplyModifiedProperties();


    }

    private string[] FetchFilesInDir(string path)
    {
        string[] filesindir = { };
        if (Directory.Exists(path))
        {
            filesindir = Directory.GetFiles(path, "*.npz", SearchOption.TopDirectoryOnly);
            if (filesindir.Length == 0)
            {
                filesindir = RecursiveFetchFilesInDir(path, 5);
            }
        }
        else if (File.Exists(path) && path.EndsWith(".npz"))
            filesindir = new string[] { path };
        return filesindir;
    }

    private string[] RecursiveFetchFilesInDir(string path, int maxdepth, int depth = 0)
    {
        if (depth > maxdepth) return new string[] { };
        List<string> filesindir = new List<string>();
        
        foreach (string dir in Directory.GetDirectories(path))
        {
            var files = Directory.GetFiles(dir, "*.npz", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                filesindir.AddRange(RecursiveFetchFilesInDir(dir, maxdepth, depth + 1).ToList());
            }
            else
                filesindir.Add(files.First());

        }
        return filesindir.ToArray();
    }
}
