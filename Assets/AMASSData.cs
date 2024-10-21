using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using static NPArrayHelper;



/// load AMASS dataset
public class AMASSData
{
    
    public string FileName{ get; protected set; }
    public bool LoadedOnlyPoses { get; protected set; }

    

    float[,] _trans;
    double _mocap_framerate;
    Quaternion[,] _poseQ;

    // minimum need to play : framerate, rmatQ, trans
    Quaternion[,] _unity_pose_rmatQ; // predicted full body joints local rotation matrix to quaternion
    float[,] _unity_trans; // predicted root joint translation
    Quaternion[,] _gt_unity_pose_rmatQ; // Ground truth full body joints local rotation matrix to quaternion
    float[,] _gt_unity_trans; // Ground truth root joint translation

    // additional data : vertex position, full body joint global position
    float[,] _unity_position_global; // predicted global position of each joint
    float[,] _gt_unity_position_global; // Ground truth global position of each joint
    float[,] _bm_vert_pos; // predicted body mesh vertex positions
    float[,] _gt_bm_vert_pos; // Ground truth body mesh vertex positions

    public int FrameCount => _unity_pose_rmatQ?.GetLength(0) ?? _unity_trans.GetLength(0);
    public double FrameRate => _mocap_framerate;
    public double PlayTime => FrameCount / FrameRate;
    public bool HasGT => _gt_unity_pose_rmatQ != null;

    public class AMASSPose
    {
        public Vector3 root_translation;
        public Quaternion[] pose_body;
    }

    static public AMASSData LoadVROnly(string fileName) => new AMASSData(fileName, true, true);

    // default constructor, only load data
    AMASSData(string fileName, bool vrOnly, bool _)
    {
        LoadedOnlyPoses = false;
        using (ZipArchive archive = new ZipArchive(new FileStream(fileName, FileMode.Open)))
        {
            if (archive == null)
                throw new ArgumentException("Zip archive is null, file not found? " + fileName);
            if (archive.Entries.Count == 0)
                throw new ArgumentException("Zip archive is empty: " + fileName);
        }
    }
    
    // load additional data : true ( load vertex, position data )
    // load additional data : false ( only load minimum data )
    public AMASSData(string fileName, bool load_additional_data = false)
    {
        using (ZipArchive archive = new ZipArchive(new FileStream(fileName, FileMode.Open)))
        {
            if (archive == null)
                throw new ArgumentException("Zip archive is null, file not found? " + fileName);
            if (archive.Entries.Count == 0)
                throw new ArgumentException("Zip archive is empty: " + fileName);

            if (!load_additional_data)
                LoadMinimum(archive);
            else
            {
                LoadMinimum(archive);
                LoadPositionData(archive);
                LoadVertexData(archive);
            }
        }
    }
    private void LoadMinimum(ZipArchive archive)
    {
        try
        {
            _mocap_framerate = ZippedNPY1DArrType<float>(archive, "mocap_framerate.npy")[0];
        }
        catch (ArrayTypeMismatchException)
        {
            _mocap_framerate = 60;
            Debug.Log("failed to load mocap_framerate, assuming 60fps");
        }
        
        _unity_pose_rmatQ = Rotmat_Arr_to_QuaternionArr(ZippedNPY2DArrType<float>(archive, "unity_pose_rmat.npy"));
        _unity_trans = ZippedNPY2DArrType<float>(archive, "unity_trans.npy");

        try
        {
            _gt_unity_pose_rmatQ = Rotmat_Arr_to_QuaternionArr(ZippedNPY2DArrType<float>(archive, "gt_unity_pose_rmat.npy"));
            _gt_unity_trans = ZippedNPY2DArrType<float>(archive, "gt_unity_trans.npy");
        }
        catch (Exception) {
            Debug.LogWarning("Could not find GT values from file.");
        }
    }

    private void LoadPositionData(ZipArchive archive)
    {
        try
        {
            _unity_position_global = ZippedNPY2DArrType<float>(archive, "unity_position.npy");
            _gt_unity_position_global = ZippedNPY2DArrType<float>(archive, "gt_unity_position.npy");
        }
        catch (Exception)
        {
            Debug.LogWarning("Could not find global position values from file.");
        }
    }

    private void LoadVertexData(ZipArchive archive)
    {
        try
        {
            _bm_vert_pos = ZippedNPY2DArrType<float>(archive, "pred_bm_vert.npy");
            _gt_bm_vert_pos = ZippedNPY2DArrType<float>(archive, "gt_bm_vert.npy");
        }
        catch (Exception)
        {
            Debug.LogWarning("Could not find bm vertex data from file");
        }
    }
    
    public AMASSPose GetUnityCoordDatas(int frame, bool useGT)
    {
        var t = new Vector3(_unity_trans[frame, 0], _unity_trans[frame, 1], _unity_trans[frame, 2]);
        var p = (from i in Enumerable.Range(0, 22) select _unity_pose_rmatQ[frame, i]).ToArray();
        if (useGT)
        { 
            t = new Vector3(_gt_unity_trans[frame, 0], _gt_unity_trans[frame, 1], _gt_unity_trans[frame, 2]);
            p = (from i in Enumerable.Range(0, 22) select _gt_unity_pose_rmatQ[frame, i]).ToArray();
        }    

        return new AMASSPose()
        {
            root_translation = t,
            pose_body = p
        };
    }

    public List<(Vector3 trans, Quaternion rot)> GetGTGlobalPoses(int frame)
    {
        var poses = new List<(Vector3 trans, Quaternion rot)>();

        for (int joint = 0; joint < 22; joint++)
        {
            var trans = new Vector3(
                            _gt_unity_position_global[frame, joint * 3 + 0],
                            _gt_unity_position_global[frame, joint * 3 + 1],
                            _gt_unity_position_global[frame, joint * 3 + 2]);
            var rot = _gt_unity_pose_rmatQ[frame, joint];
            poses.Add((trans, rot));
        }
        return poses;
    }

    public List<(Vector3 trans, Quaternion rot)> GetPredGlobalPoses(int frame)
    {
        var poses = new List<(Vector3 trans, Quaternion rot)>();

        for (int joint = 0; joint < 22; joint++)
        {
            var trans = new Vector3(
                            _unity_position_global[frame, joint * 3 + 0],
                            _unity_position_global[frame, joint * 3 + 1],
                            _unity_position_global[frame, joint * 3 + 2]);
            var rot = _unity_pose_rmatQ[frame, joint];

            poses.Add((trans, rot));
        }
        return poses;
    }

    public List<Vector3> GetPredVert(int frame, int interval=10)
    {
        var vert_positions = new List<Vector3>();

        for (int i = 0; i < 6890; i+=interval)
        {
            vert_positions.Add(new Vector3(_bm_vert_pos[frame, i * 3 + 0],
            _bm_vert_pos[frame, i * 3 + 1],
            _bm_vert_pos[frame, i * 3 + 2]));
        }
        return vert_positions;
    }

    public List<Vector3> GetGTVert(int frame, int interval=10)
    {
        var vert_positions = new List<Vector3>();

        for (int i = 0; i < 6890; i+=interval)
        {
            vert_positions.Add(new Vector3(_gt_bm_vert_pos[frame, i * 3 + 0],
            _gt_bm_vert_pos[frame, i * 3 + 1],
            _gt_bm_vert_pos[frame, i * 3 + 2]));
        }
        return vert_positions;
    }

    #region GizmoDrawers
#if UNITY_EDITOR

    List<string> joint_names = new List<string>() {
                "root",
                "left_hip", "right_hip", "spine_1", // 1,2,3
                "left_knee", "right_knee", "spine_2", // 4,5,6
                "left_ankle", "right_ankle", "spine_3", // 7,8,9
                "left_foot", "right_foot", "neck_1", // 10,11,12
                "left_clavicle", "right_clavicle", "head", // 13,14,15
                "left_shoulder", "right_shoulder", // 16,17
                "left_elbow", "right_elbow", // 18,19
                "left_wrist", "right_wrist" // 20,21
            };
    List<string> extra_joint_names = new List<string>()
    {
        "HMD", "left_controller", "right_controller"
    };
    public void DrawExtraJointGizmos(int frame, bool isAvatarCoordinate)
    {
        if (isAvatarCoordinate)
            DrawJointGizmos(frame, true, GizmoType.EJ_Avatar);
        else
            DrawJointGizmos(frame, true, GizmoType.EJ_World);
    }
    public void DrawAvatarspaceJoints(int frame, bool isAvatarCoordinate)
    {
        if (isAvatarCoordinate)
            DrawJointGizmos(frame, true, GizmoType.ALL_Avatarspace);
        else
            DrawJointGizmos(frame, true, GizmoType.ALL_Avatar_World);
    }

    public enum GizmoType
    {
        ALL_Global_World, EJ_Avatar, EJ_World, ALL_Avatarspace, ALL_Avatar_World
    }
    
    void DrawJointGizmos(int frame, bool asArrows = false, GizmoType gizmoType = GizmoType.EJ_Avatar)
    {
        int jointCount;
        List<string> jNames;

        jointCount = 22;
        jNames = joint_names;

        for (int joint = 0; joint < jointCount; joint++)
        {
            Vector3 trans;
            Quaternion rot;

            trans = new Vector3(
                _unity_trans[frame, joint * 3 + 0],
                _unity_trans[frame, joint * 3 + 1],
                _unity_trans[frame, joint * 3 + 2]);
            rot = _unity_pose_rmatQ[frame, joint];

            DrawTransRot(trans, rot, false, asArrows);
        }
        DrawTransRot(Vector3.zero, Quaternion.identity, false, true);
    }
    public void DrawTransRot(Vector3 origin, Quaternion rotation, bool drawPoint, bool drawRot)
    {
        GUIStyle uiStyle = new GUIStyle();
        uiStyle.normal.background = Texture2D.whiteTexture;
        uiStyle.normal.textColor = Color.black;
        if (drawPoint)
            DebugExtension.DebugWireSphere(origin, new Color(0, 1, 1), 0.05f);
        if (drawRot)
        {
            float length = 0.15f;
            DebugExtension.DebugArrow(origin, rotation * Vector3.right * length, Color.red);
            DebugExtension.DebugArrow(origin, rotation * Vector3.up * length, new Color(0, .6f, 0));
            DebugExtension.DebugArrow(origin, rotation * Vector3.forward * length, Color.blue);
        }
        
    }
    #endif
    #endregion

    
}
