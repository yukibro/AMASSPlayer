using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using System.Text;
using static NPArrayHelper;
using System.IO;
using JetBrains.Annotations;
using static UnityEngine.UIElements.UxmlAttributeDescription;
using Valve.Newtonsoft.Json.Bson;
using Valve.VR;
using UnityEditor.UIElements;
using System.Threading;


public class AMASSDataPlayer : ThingWithAvatarHiarchy
{

    [SerializeField] private string _filename;
    private AMASSData _amassData;
    [SerializeField] private GameObject main_model;
    private SkinnedMeshRenderer mainModelRenderer;
    

    public bool IsPlaying { get; private set; }
    public bool Loop { get; set; } = true;
    [InspectorName("Frame No.(ReadOnly)")]
    public float _frame = 0;
    private int _frameCount = 0;
    public int FrameCount => _frameCount;
    private double _frameRate = 60;
    public double FrameRate { get => FrameRate; }
    public bool Ready => _amassData != null;
    public int FrameNo { get => (int)_frame; }
    
    public bool IsFinished => _frame >= _frameCount;

    [SerializeField] bool drawJointTransforms = false;
    [SerializeField] bool drawJointHeirarchy = false;
    [SerializeField] bool jointName = false;
    [SerializeField] bool showGT = true;
    [SerializeField] bool showJointColorError = false;
    
    [SerializeField]Gradient jointGradient;
    [SerializeField]
    float gradmax = 0.3f;
    [SerializeField]
    float gradmin = 0.0f;

    // vertex error
    [SerializeField] bool LOAD_ADDITIONAL_DATA = false;
    [SerializeField] bool showMeshColorError = false;
    [SerializeField] bool showPredVertex = false;
    [SerializeField] bool showGTVertex = false;

    // Jitter, Skating error
    [SerializeField] bool showJitterLine = false;
    [SerializeField] bool showSkating = false;
    [SerializeField] bool showVertexTrajectory = false;
    [SerializeField] float trajectoryUpdateInterval = 1/60f;
    [SerializeField] int trajectoryWindow = 40;
    [SerializeField] Color jitterLineColor = new Color(1, 0.4f, 0);
    [SerializeField] Color skatingLineColor = new Color(0, 0, 0);
    [SerializeField] ColorCodedTexController textureController;
    

    List<(string name, Vector3 translation, Quaternion rotation)> _originalJointTransforms;
    List<Vector3> joint_trajectory_list = new List<Vector3>();
    List<Vector3> foot_joint_trajectory_list = new List<Vector3>();
    List<Vector3> vertex_trajectory_list = new List<Vector3>();
    float update_timer = 0f;

    private void OnGUI() 
    {
        Handles.BeginGUI();
        GUI.Label(new Rect(10, 10, 100, 20), "Frame: " + _frame);
        Handles.EndGUI();
    }

    List<ColorCodedTexController> colorCodedTexControllers;
    private void Awake(){
        // set the timespeed
        Time.timeScale = 1;
        if (main_model != null)
        {
            mainModelRenderer = main_model.GetComponent<SkinnedMeshRenderer>();
            mainModelRenderer.rendererPriority = 100;
            
        // Save the original joint position and rotation
        _originalJointTransforms = new List<(string, Vector3, Quaternion)>();
        foreach(var jt in _jointTransforms) {
            if(jt != null) {
                _originalJointTransforms.Add((jt.name, jt.localPosition, jt.localRotation));
            }
        }

        StringBuilder sb = new StringBuilder();
        for(int i = 0; i < _originalJointTransforms.Count; i++) {
            var jt = _originalJointTransforms[i];
            sb.AppendLine($"{i},{jt.name},{jt.translation.x},{jt.translation.y},{jt.translation.z}");
        }

        // get all colorCodedTexControllers
        colorCodedTexControllers = new List<ColorCodedTexController>();

        foreach (var cc in FindObjectsOfType<ColorCodedTexController>())
        {
            colorCodedTexControllers.Add(cc);
        }
        }
    }
    public void Start()
    {
        LoadAnimation();

        //check joint transforms if it has duplicated element
        var duplicate = _jointTransforms.Where(g => g != null && _jointTransforms.Count(x => x == g) > 1).ToList();
        if (duplicate.Count > 0)
        {
            Debug.LogError("There are duplicated elements in jointTransforms");
            Debug.LogError(duplicate.Count + " duplicated elements found");
            foreach (var d in duplicate)
            {
                Debug.LogError($"duplicated element: {d.name}");
            }
        }
    }

    public void Play()
    {
        IsPlaying = true;
    }
    public void Pause()
    {
        IsPlaying = false;
    }
    
    public void LoadAnimation(string _filename = null)
    {
        _filename = _filename ?? this._filename;
        this. _filename = _filename;
        
        if (!File.Exists(_filename))
        {
            Debug.Log($"cannot find {_filename}");
            return;
        }
        _amassData = new AMASSData(_filename, LOAD_ADDITIONAL_DATA);
        _frameCount = _amassData.FrameCount;
        _frameRate = _amassData.FrameRate;
        _frame = 0;
    }
    [SerializeField]
    ThingWithAvatarHiarchy trailingModel;
    [SerializeField] int trailingGenTime = 150;
    [SerializeField] int numTrailAvatar = 10;
    [SerializeField] float trailingAvatarInterval = 0.01f;
    [SerializeField] bool enableTrailing = true;
    [SerializeField] Gradient trailingGradient;
    List<(GameObject go, ColorCodedTexController texController)> trailing = new List<(GameObject go, ColorCodedTexController texController)>();
    [SerializeField] Transform trailingParent;

    int lastCapturedtrailingFrame;

    private List<(Vector3 position, Quaternion rotation)> trailingTransforms = new List<(Vector3, Quaternion)>();

    private List<Vector3> mainModelPositions = new List<Vector3>();
    public void UpdateTrailing(int frame)
    {
        if (!enableTrailing) return;
        Vector3 currentMainModelPosition = trailingModel.transform.position; // save the current position of the main model
        if (lastCapturedtrailingFrame == -1 || frame - lastCapturedtrailingFrame >= trailingGenTime) // update frame
        {
            lastCapturedtrailingFrame = frame;
            GameObject newtrailing = Instantiate(trailingModel.gameObject); // create a new trailing model
            newtrailing.transform.SetParent(trailingParent); // set the parent

            var allscripts = newtrailing.GetComponentsInChildren<MonoBehaviour>(); 

            ColorCodedTexController texController = null;

            foreach (var script in allscripts) // disable all scripts
            {
                if (script is ColorCodedTexController controller) texController = controller;
                script.enabled = false;
            }
            Debug.Assert(texController != null);
            trailing.Add((newtrailing, texController));
            mainModelPositions.Add(currentMainModelPosition);
            if (trailing.Count > numTrailAvatar) // destroy the oldest one
            {
                GameObject.Destroy(trailing[0].go);
                trailing.RemoveAt(0);
                mainModelPositions.RemoveAt(0);
            }
        }

        List<Vector3> zeros = new List<Vector3>();
        for (int i = 0; i < 22; i++)
        {
            zeros.Add(Random.insideUnitSphere);
            }

        for (int idx = 0; idx < trailing.Count; idx++) // setting color and position
        {
            int rel_idx = trailing.Count - idx;
            
            trailing[idx].go.SetActive(true);
            Color c = trailingGradient.Evaluate(1f - (float)rel_idx / numTrailAvatar);
            Vector3 vecCol = new Vector3(c.r, c.g, c.b);
            List<Vector3> oneColorArray = new List<Vector3>();
            for (int i = 0; i < 22; i++)
            {
                oneColorArray.Add(vecCol);
            }
                
            trailing[idx].go.transform.position = mainModelPositions[idx];
            trailing[idx].texController.UpdateShader(zeros, oneColorArray);
        }
    }

    void UpdateJointTrajectory()
    {
        if (!showJitterLine) return;
        if (_amassData == null) return;

        if (update_timer >= trajectoryUpdateInterval)
        {
            for (int i = 0; i < 22; i++)
            {
                joint_trajectory_list.Add(_jointTransforms[i].position);
            }
            if (joint_trajectory_list.Count > trajectoryWindow*22)
            {
                joint_trajectory_list.RemoveRange(0, 22);
            }
        }

        for (int i = 0; i < joint_trajectory_list.Count; i++)
        {
            // Draw only lower body joints
            if (i == 0 || i == 1 || i == 2 || i == 4 || i == 5 || i == 7 || i == 8 || i == 10 || i == 11)
                DebugExtension.DebugWireSphere(joint_trajectory_list[i], jitterLineColor, 0.01f);
        }
        for (int i = 0; i < joint_trajectory_list.Count; i += 22)
        {
            DrawLinks(joint_trajectory_list.GetRange(i, 22).ToArray());
        }
    }

    void UpdateFootJointTrajectory()
    {
        if (!showSkating) return;
        if (_amassData == null) return;

        if (update_timer >= trajectoryUpdateInterval)
        {
            foot_joint_trajectory_list.Add(_jointTransforms[10].position);
            foot_joint_trajectory_list.Add(_jointTransforms[11].position);
            if (foot_joint_trajectory_list.Count > trajectoryWindow*2)
            {
                foot_joint_trajectory_list.RemoveRange(0, 2);
            }
        }

        for (int i = 0; i < foot_joint_trajectory_list.Count; i++)
        {
            DebugExtension.DebugWireSphere(foot_joint_trajectory_list[i], skatingLineColor, 0.01f);
        }

        for (int i = 0; i < foot_joint_trajectory_list.Count - 2; i += 2)
        {
        Debug.DrawLine(foot_joint_trajectory_list[i], foot_joint_trajectory_list[i + 2], skatingLineColor);
        Debug.DrawLine(foot_joint_trajectory_list[i + 1], foot_joint_trajectory_list[i + 3], skatingLineColor);
        }
        
    }
    
    // Update the mesh error shaders, need additional data (full body position)
    void UpdateJointErrorShaders()
    {
        if (!showJointColorError) return;
        if (colorCodedTexControllers == null) return;
        if (_amassData.GetGTGlobalPoses((int)_frame) == null)
        {
            Debug.LogWarning("GTGlobalPoses is null");
            return;
        }
        List<Vector3> colors = new List<Vector3>();
        List<Vector3> joint_gt = null;
        List<Vector3> jPositions= null;
        if (_amassData.HasGT == true)
        {
            joint_gt = (from (Vector3 p, Quaternion r) pair in _amassData.GetGTGlobalPoses((int)_frame) select pair.p).ToList();
            jPositions = (from (Vector3 p, Quaternion r) pair in _amassData.GetPredGlobalPoses((int)_frame) select pair.p).ToList();
            for (int i = 0; i < 22; i++)
            {
                var delta = joint_gt[i] - jPositions[i];
                var dist = delta.magnitude;

                var val = (dist - gradmin) / (gradmax - gradmin);
                if (val < 0) val = 0;
                if (val > 1) val = 1;
                Color color = jointGradient.Evaluate(val);
                Vector3 colAsVec3 = new Vector3(color.r, color.g, color.b);

                colors.Add(colAsVec3);
            }
        }
        else
        {
            joint_gt = null;
            Color color = jointGradient.Evaluate(0);
            Vector3 colAsVec3 = new Vector3(color.r, color.g, color.b);
            for (int i = 0; i < 22; i++)
                colors.Add(colAsVec3);
        }

        foreach(var cc in colorCodedTexControllers)
            if (cc.isActiveAndEnabled)
                cc.UpdateShader(jPositions, colors);
    }
    
    void UpdateMeshErrorShaders()
    {
        if (!showMeshColorError) return;
        if (colorCodedTexControllers == null) return;
        if (_amassData == null) return;
        if (_amassData.HasGT == false) return;
        if (LOAD_ADDITIONAL_DATA == false)
        {
            Debug.LogWarning("LOAD_ADDITIONAL_DATA is false");
            return;
        }
        if (_amassData.GetGTVert((int)_frame) == null)
        {
            Debug.LogWarning("Vertex data is null");
            return;
        }

        List<Vector3> colors = new List<Vector3>();
        List<Vector3> gt_mesh_vert = null;
        List<Vector3> pred_mesh_vert= null;

        gt_mesh_vert = _amassData.GetGTVert((int)_frame);
        pred_mesh_vert = _amassData.GetPredVert((int)_frame);

        if(showPredVertex)
        {
            for (int i = 0; i < pred_mesh_vert.Count; i++)
            {
                DebugExtension.DebugWireSphere(pred_mesh_vert[i], Color.red, 0.005f);
            }
        }

        if(showGTVertex)
        {
            for (int i = 0; i < gt_mesh_vert.Count; i++)
            {
                DebugExtension.DebugWireSphere(gt_mesh_vert[i], Color.blue, 0.005f);
            }
        }

        for (int i = 0; i < gt_mesh_vert.Count; i++)
        {
            var delta = gt_mesh_vert[i] - pred_mesh_vert[i];
            var dist = delta.magnitude;

            var val = (dist - gradmin) / (gradmax - gradmin);
            if (val < 0) val = 0;
            if (val > 1) val = 1;
            Color color = jointGradient.Evaluate(val);
            Vector3 colAsVec3 = new Vector3(color.r, color.g, color.b);

            colors.Add(colAsVec3);
        }

        foreach(var cc in colorCodedTexControllers)
            if (cc.isActiveAndEnabled)
            {
                cc.UpdateShader(pred_mesh_vert, colors);
            }
    }

    void UpdateVertexTrajectory()
    {
        if (!showVertexTrajectory) return;
        if (_amassData == null) return;
        if (LOAD_ADDITIONAL_DATA == false)
        {
            Debug.LogWarning("LOAD_ADDITIONAL_DATA is false");
            return;
        }

        int interval = 100;

        if (showGT)
        {
            vertex_trajectory_list.AddRange(_amassData.GetGTVert((int)_frame, interval));
        }
            
        else
            vertex_trajectory_list.AddRange(_amassData.GetPredVert((int)_frame, interval));
        
        if (vertex_trajectory_list.Count > trajectoryWindow*(int)(6890/interval))
        {
            vertex_trajectory_list.RemoveRange(0, (int)(6890/interval));
        }

        for (int i = 0; i < vertex_trajectory_list.Count; i++)
        {
            DebugExtension.DebugWireSphere(vertex_trajectory_list[i], jitterLineColor, 0.001f);
        }
    }

    public void Update()
    {
        if (IsPlaying)
        {
            _frame += Time.deltaTime * (float)_frameRate;
            
        }
        if (_frame >= _frameCount - 1) 
        {
            _frame = _frameCount - 1;
        }

        update_timer += Time.deltaTime;
        UpdateTrailing(FrameNo);
        UpdateJointErrorShaders();
        UpdateMeshErrorShaders();
        UpdateJointTrajectory();
        UpdateFootJointTrajectory();
        UpdateVertexTrajectory();
        if (update_timer >= trajectoryUpdateInterval)
            update_timer = 0;
        AMASSData.AMASSPose frameData;

        frameData = _amassData.GetUnityCoordDatas((int)_frame, showGT);

        // update the joint transforms
        _jointTransforms[0].localPosition = frameData.root_translation;

        for (int i = 0; i < _jointTransforms.Count; i++) 
        {
            if(_jointTransforms[i] != null) // end effector joint may be null
            {
              _jointTransforms[i].localRotation = _originalJointTransforms[i].rotation * frameData.pose_body[i];
            }
               
        }
    }

    void DrawLinks(Vector3[] _joints)
    {
        //Draw heierarchy of joint transforms.
        //0>1 0>2 0>3
        Debug.DrawLine(_joints[0], _joints[1], jitterLineColor);
        Debug.DrawLine(_joints[0], _joints[2], jitterLineColor);
        //1>4 2>5 3>6
        Debug.DrawLine(_joints[1], _joints[4], jitterLineColor);
        Debug.DrawLine(_joints[2], _joints[5], jitterLineColor);
        //4>7 4>8 6>9
        Debug.DrawLine(_joints[4], _joints[7], jitterLineColor);
        Debug.DrawLine(_joints[5], _joints[8], jitterLineColor);
        //7>10 8>11
        Debug.DrawLine(_joints[7], _joints[10], jitterLineColor);
        Debug.DrawLine(_joints[8], _joints[11], jitterLineColor);
    }

    public void OnDrawGizmos()
    {
        DrawHiarchy();
        DrawJointNames();
        DrawJointTransform();

        void DrawHiarchy()
        {
            if (!drawJointHeirarchy) return;
            //Draw heierarchy of joint transforms.
            //0>1 0>2 0>3
            DebugExtension.DebugArrow(_jointTransforms[0].position, _jointTransforms[1].position - _jointTransforms[0].position, Color.red);
            DebugExtension.DebugArrow(_jointTransforms[0].position, _jointTransforms[2].position - _jointTransforms[0].position, Color.green);
            DebugExtension.DebugArrow(_jointTransforms[0].position, _jointTransforms[3].position - _jointTransforms[0].position, Color.blue);
            //1>4 2>5 3>6
            DebugExtension.DebugArrow(_jointTransforms[1].position, _jointTransforms[4].position - _jointTransforms[1].position, Color.red);
            DebugExtension.DebugArrow(_jointTransforms[2].position, _jointTransforms[5].position - _jointTransforms[2].position, Color.green);
            DebugExtension.DebugArrow(_jointTransforms[3].position, _jointTransforms[6].position - _jointTransforms[3].position, Color.blue);
            //4>7 4>8 6>9
            DebugExtension.DebugArrow(_jointTransforms[4].position, _jointTransforms[7].position - _jointTransforms[4].position, Color.red);
            DebugExtension.DebugArrow(_jointTransforms[5].position, _jointTransforms[8].position - _jointTransforms[5].position, Color.green);
            DebugExtension.DebugArrow(_jointTransforms[6].position, _jointTransforms[9].position - _jointTransforms[6].position, Color.blue);

            //7>10 8>11
            DebugExtension.DebugArrow(_jointTransforms[7].position, _jointTransforms[10].position - _jointTransforms[7].position, Color.red);
            DebugExtension.DebugArrow(_jointTransforms[8].position, _jointTransforms[11].position - _jointTransforms[8].position, Color.green);
        }

        void DrawJointTransform()
        {
            if (!drawJointTransforms) return;
            //Draw 3d orientation of all joints
            float size = 0.1f;
            for (int i = 0; i < _jointTransforms.Count; i++)
            {
                if (_jointTransforms[i] != null)
                {
                    var rot = _jointTransforms[i].rotation;
                    DebugExtension.DebugArrow(_jointTransforms[i].position, rot * Vector3.right * size, Color.red);
                    DebugExtension.DebugArrow(_jointTransforms[i].position, rot * Vector3.up * size, Color.green);
                    DebugExtension.DebugArrow(_jointTransforms[i].position, rot * Vector3.forward * size, Color.blue);
                }
            }
        }

        void DrawJointNames()
        {
            if(!jointName) return;

            //Draw name of joint transforms
            var names = new List<string>() {
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
            GUIStyle textStyle = new GUIStyle();
            textStyle.normal.textColor = Color.black;
            textStyle.normal.background = Texture2D.whiteTexture;
            for (int i = 0; i < _jointTransforms.Count; i++)
            {
                
                if (_jointTransforms[i] != null)
                    Handles.Label(_jointTransforms[i].position, $"{i}: {names[i]}", textStyle);
            }
        }
    }
}