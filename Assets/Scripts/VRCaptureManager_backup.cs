using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Management;
using System.Collections;
using Unity.Mathematics;
using System.IO;
using UnityEditor;
using System;
using NumSharp;
using System.IO.Compression;
using Valve.VR;


public class VRCaptureManager_backup : MonoBehaviour
{
    [SerializeField] protected List<Transform> _jointTransforms;
    public List<Transform> JointTransfroms { get => _jointTransforms; }
    public int target_framerate;
    public string target_filename;
    [SerializeField]

    private bool calibrated = false;

    //float ScaleMultiplier => height_cm / ref_height_cm;

    [SerializeField]
    Transform hmd, l_controller, r_controller;
    private Quaternion hmdRotOffset;

    private Matrix4x4 initial_global_matrix_l_M;
    private Matrix4x4 initial_global_matrix_r_M;
    private Matrix4x4 initial_wrist_matrix_l_W;
    private Matrix4x4 initial_wrist_matrix_r_W;
    private List<Matrix4x4> frames;
    
    void Start()
    {
        calibrated = false;    
        frames = new List<Matrix4x4>();
        Application.targetFrameRate = target_framerate;
    }

    void Update() {
    if (SteamVR_Actions.default_GrabPinch.GetStateDown(SteamVR_Input_Sources.Any) && !calibrated)
    {
        Debug.Log("Calibration Start");
        CalibrateTrackers();
    }
    
    if (calibrated)
    {
        
        CollectFrame();
    }

    }
    void OnApplicationQuit()
    {
        Save();
    }
    
    
    void CollectFrame()
    {
        frames.Add(hmd.localToWorldMatrix);
        frames.Add(l_controller.localToWorldMatrix*(Matrix4x4.TRS(l_controller.localPosition, l_controller.localRotation, Vector3.one) * initial_global_matrix_l_M.transpose * initial_wrist_matrix_l_W));
        frames.Add(r_controller.localToWorldMatrix*(Matrix4x4.TRS(r_controller.localPosition, r_controller.localRotation, Vector3.one) * initial_global_matrix_r_M.transpose * initial_wrist_matrix_r_W));
    }
    //Validate if file exists, if so, ask if overwrite

    void CalibrateTrackers()
    {
        //// HMD, LR Controllers position offset
        // CalibrateHeight();

        //// HMD rotation offset

        hmdRotOffset = _jointTransforms[15].rotation * Quaternion.Inverse(hmd.rotation);

        //// Initial XYZ global axis of controller
        Vector3 l_controller_x = l_controller.right;
        Vector3 l_controller_y = l_controller.up;
        Vector3 l_controller_z = l_controller.forward;

        Vector3 r_controller_x = r_controller.right;
        Vector3 r_controller_y = r_controller.up;
        Vector3 r_controller_z = r_controller.forward;

        //// Initial XYZ global axis of wrist
        Vector3 l_wrist_x = _jointTransforms[20].right;
        Vector3 l_wrist_y = _jointTransforms[20].up;
        Vector3 l_wrist_z = _jointTransforms[20].forward;

        Vector3 r_wrist_x = _jointTransforms[21].right;
        Vector3 r_wrist_y = _jointTransforms[21].up;
        Vector3 r_wrist_z = _jointTransforms[21].forward;
    
        //// Make it to homogenous matrix
        initial_global_matrix_l_M = Matrix4x4.identity;
        initial_global_matrix_l_M.SetColumn(0, new Vector4(l_controller_x.x, l_controller_x.y, l_controller_x.z, 0));
        initial_global_matrix_l_M.SetColumn(1, new Vector4(l_controller_y.x, l_controller_y.y, l_controller_y.z, 0));
        initial_global_matrix_l_M.SetColumn(2, new Vector4(l_controller_z.x, l_controller_z.y, l_controller_z.z, 0));

        initial_global_matrix_r_M = Matrix4x4.identity;
        initial_global_matrix_r_M.SetColumn(0, new Vector4(r_controller_x.x, r_controller_x.y, r_controller_x.z, 0));
        initial_global_matrix_r_M.SetColumn(1, new Vector4(r_controller_y.x, r_controller_y.y, r_controller_y.z, 0));
        initial_global_matrix_r_M.SetColumn(2, new Vector4(r_controller_z.x, r_controller_z.y, r_controller_z.z, 0));

        initial_wrist_matrix_l_W = Matrix4x4.identity;
        initial_wrist_matrix_l_W.SetColumn(0, new Vector4(l_wrist_x.x, l_wrist_x.y, l_wrist_x.z, 0));
        initial_wrist_matrix_l_W.SetColumn(1, new Vector4(l_wrist_y.x, l_wrist_y.y, l_wrist_y.z, 0));
        initial_wrist_matrix_l_W.SetColumn(2, new Vector4(l_wrist_z.x, l_wrist_z.y, l_wrist_z.z, 0));

        initial_wrist_matrix_r_W = Matrix4x4.identity;
        initial_wrist_matrix_r_W.SetColumn(0, new Vector4(r_wrist_x.x, r_wrist_x.y, r_wrist_x.z, 0));
        initial_wrist_matrix_r_W.SetColumn(1, new Vector4(r_wrist_y.x, r_wrist_y.y, r_wrist_y.z, 0));
        initial_wrist_matrix_r_W.SetColumn(2, new Vector4(r_wrist_z.x, r_wrist_z.y, r_wrist_z.z, 0));

        calibrated = true;
    }

    void UpdateTrackersOffset()
    {
        Matrix4x4 l_matrix = Matrix4x4.TRS(l_controller.localPosition, l_controller.localRotation, Vector3.one) * initial_global_matrix_l_M.transpose * initial_wrist_matrix_l_W;
        Matrix4x4 r_matrix = Matrix4x4.TRS(r_controller.localPosition, r_controller.localRotation, Vector3.one) * initial_global_matrix_r_M.transpose * initial_wrist_matrix_r_W;

        hmd.localRotation = hmd.localRotation;
        l_controller.transform.localRotation = l_matrix.rotation;
        r_controller.transform.localRotation = r_matrix.rotation;
    }

    void ValidateTargetFile()
    { 
        while (File.Exists(target_filename))
        {
            //open dialogue
            bool overwrite = EditorUtility.DisplayDialog(
                                                "File already exists",
                                                $"File \"{target_filename}\" already exists,\nOverwrite?",
                                                "Yes", "No(select new dir)");
            if (overwrite == true)
            {
                if (EditorUtility.DisplayDialog("Confirm", "Confirm Overwrite", "Overwrite", "Cancel"))
                {
                    File.Delete(target_filename);
                    break;
                }
            }
            else if (overwrite == false)
                target_filename = EditorUtility.SaveFilePanel("Save capture file as", "", target_filename, ".npz");
        }
    }

    void Mat2Rotmatarr(float[,] arr, int2 pos, Matrix4x4 matrix)
    {
        arr[pos.x, pos.y + 0] = matrix.m00;
        arr[pos.x, pos.y + 1] = matrix.m01; 
        arr[pos.x, pos.y + 2] = matrix.m02;
        arr[pos.x, pos.y + 3] = matrix.m10; 
        arr[pos.x, pos.y + 4] = matrix.m11; 
        arr[pos.x, pos.y + 5] = matrix.m12;
        arr[pos.x, pos.y + 6] = matrix.m20; 
        arr[pos.x, pos.y + 7] = matrix.m21; 
        arr[pos.x, pos.y + 8] = matrix.m22;
    }

    void Mat2Translationarr(float[,] arr, int2 pos, Matrix4x4 matrix)
    {
        arr[pos.x, pos.y + 0] = matrix.m03;
        arr[pos.x, pos.y + 1] = matrix.m13;
        arr[pos.x, pos.y + 2] = matrix.m23;
    }
    // void Vec32Translationarr(float[,] arr, int2 pos, Vector3 v3)
    // {
    //     v3 *= ScaleMultiplier;
    //     arr[pos.x, pos.y + 0] = v3.x; arr[pos.x, pos.y + 1] = v3.y; arr[pos.x, pos.y + 2] = v3.z;
    // }


    void Write2DArrToZip_npy(ZipArchive target, string name, Array arr)
    {
        //create file in archive
        var entry = target.CreateEntry(name);
        //create stream
        using (var stream = entry.Open())
        {
            NumSharp.np.Save(arr, stream);
        }
    }

    // save all frames to file
    void Save()
    {
    Debug.Assert(frames.Count % 3 == 0);
    int length = frames.Count / 3;

    ValidateTargetFile();

    float[,] controller_rot_global = new float[length, 27];
    float[,] controller_trans_global = new float[length, 9];

    EditorUtility.DisplayProgressBar("Processing frames", "Saving", 0.0f);

    for (int frame = 0; frame < length; frame++)
    {
        EditorUtility.DisplayProgressBar("Processing frames", "Saving", (float)frame / length);

        if (frame * 3 + 2 >= frames.Count)
        {
            Debug.LogError("Frame index out of range.");
            break;
        }

        Mat2Rotmatarr(controller_rot_global, new int2(frame, 0), frames[frame * 3 + 0]); // 0 ~  8
        Mat2Rotmatarr(controller_rot_global, new int2(frame, 9), frames[frame * 3 + 1]); // 9 ~ 17
        Mat2Rotmatarr(controller_rot_global, new int2(frame, 18), frames[frame * 3 + 2]);// 18 ~ 26

        Mat2Translationarr(controller_trans_global, new int2(frame, 0), frames[frame * 3 + 0]); // 0 ~ 2
        Mat2Translationarr(controller_trans_global, new int2(frame, 3), frames[frame * 3 + 1]); // 3 ~ 5
        Mat2Translationarr(controller_trans_global, new int2(frame, 6), frames[frame * 3 + 2]); // 6 ~ 8
    }

    // save array to npz

    // create zip archive
    EditorUtility.ClearProgressBar();

    using (var zip = new ZipArchive(File.Create(target_filename), ZipArchiveMode.Create))
    {
        Write2DArrToZip_npy(zip, "mocap_framerate.npy", new float[] { target_framerate });

        EditorUtility.DisplayProgressBar("Saving", "Saving controller_rot_global", 0.0f);
        Write2DArrToZip_npy(zip, "controller_rot_global.npy", controller_rot_global);

        EditorUtility.DisplayProgressBar("Saving", "Saving controller_trans_global", 0.2f);
        Write2DArrToZip_npy(zip, "controller_trans_global.npy", controller_trans_global);

        EditorUtility.DisplayProgressBar("Saving", "Done, writing to file", 1f);
    }
    EditorUtility.ClearProgressBar();
    }
}