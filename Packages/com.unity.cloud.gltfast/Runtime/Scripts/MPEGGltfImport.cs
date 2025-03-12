using GLTFast;
using GLTFast.Addons;
using GLTFast.Schema;
using System;
using System.Threading.Tasks;
using UnityEngine;
// using GltfImport = GLTFast.Newtonsoft.GltfImport;


public class MPEGGltfImport : MonoBehaviour
{

/*
    async Task Start()
    {
        try
        {
            ImportAddonRegistry.RegisterImportAddon(new MpegAddon());
            var gltfImport = new GltfImport();
            await gltfImport.Load(Uri);
            await gltfImport.InstantiateMainSceneAsync(transform);

        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
*/

    public class MpegAddon : ImportAddon<MpegAddonInstance> { }
    public class MpegAddonInstance : ImportAddonInstance
    {
        GltfImport m_GltfImport;

        public override void Dispose() { }

        public override void Inject(GltfImportBase gltfImport)
        {
            var newtonsoftGltfImport = gltfImport as GltfImport;
            if (newtonsoftGltfImport == null){
                return;
            }

            m_GltfImport = newtonsoftGltfImport;
            newtonsoftGltfImport.AddImportAddonInstance(this);
        }

        public override void Inject(IInstantiator instantiator)
        {
            var goInstantiator = instantiator as GameObjectInstantiator;
            if (goInstantiator == null){
                return;
            }
            var _ = new MpegInstantiatorAddon(m_GltfImport, goInstantiator);
        }

        public override bool SupportsGltfExtension(string extensionName)
        {
            return true;
            
            switch (extensionName)
            {
                case GLTFast.ExtensionName.SceneInteractivity:
                case GLTFast.ExtensionName.NodeInteractivity:
                case GLTFast.ExtensionName.Anchor:
                case GLTFast.ExtensionName.Media:
                case GLTFast.ExtensionName.BufferCircular:
                case GLTFast.ExtensionName.AccessorTimed:
                case GLTFast.ExtensionName.TextureVideo:
                case GLTFast.ExtensionName.SpatialAudio:
                    return true;
                
                default:
                    return false;
            }
        }
    }
}


public class MpegInstantiatorAddon 
{
    GltfImport m_GltfImport;
    GameObjectInstantiator m_Instantiator;

    public MpegInstantiatorAddon(GltfImport gltfImport, GameObjectInstantiator instantiator)
    {
        m_GltfImport = gltfImport;
        m_Instantiator = instantiator;
        m_Instantiator.NodeCreated += OnNodeCreated;
        m_Instantiator.MeshAdded += OnMeshAdded;
        m_Instantiator.EndSceneCompleted += OnEndSceneCompleted;
    }

    void OnEndSceneCompleted()
    {
        var scene = m_GltfImport.GetSourceScene(m_GltfImport.DefaultSceneIndex ?? 0);
        if (scene != null){
            ProcessMpegSceneInteractivityExtension(scene);
            ProcessMpegAnchorExtension(scene);
        }

        UpdateVirtualSceneGraph();

        m_Instantiator.NodeCreated -= OnNodeCreated;
        m_Instantiator.MeshAdded -= OnMeshAdded;
    }

    void OnNodeCreated(
        uint nodeIndex, 
        GameObject gameObject
    ){
        var gltfRoot = m_GltfImport.GetSourceRoot();

        if (VirtualSceneGraph.root != gltfRoot){
            //// IDCC
            VirtualSceneGraph.SetRoot(gltfRoot);
        }

        var node = m_GltfImport.GetSourceNode((int)nodeIndex); // as GLTFast.Newtonsoft.Schema.Node;

        if (node.Extensions?.MPEG_audio_spatial != null){
            
            MpegAudioSpatial ext = node.Extensions.MPEG_audio_spatial;

            if (ext.sources != null && (ext.sources.Length > 0))
            {
                AddAudioSources(gameObject, node);
            }
            if (ext.listener != null && (ext.listener.id >= 0))
            {
                AddAudioListener(gameObject);
            }
            if (ext.reverbs != null)
            {
                Debug.LogWarningFormat("spatial audio reverbs definition is expected on root");
            }
        }

        if (node.Extensions?.MPEG_anchor != null)
        {
            MpegAnchorObject mpegAnchor = node.Extensions.MPEG_anchor;
            // Initialized at -1 to prevent instantiation of the MPEG_anchor by the parser
            if(mpegAnchor.anchor != -1)
            {
                Debug.Log("Read node.Extensions");
                //now retrieve index in VirtualSceneGraph
                VirtualSceneGraph.AssignAnchorObjectToIndex(mpegAnchor, (int)nodeIndex);
            }
        }
            
        if(node.Extensions?.MPEG_node_interactivity != null) 
        {
            Debug.Log("Read node.extension == MPEG_node_interactivity");
        }
    }

    void UpdateVirtualSceneGraph(){
        
        for (var i = 0; i < m_GltfImport.TextureCount; i++)
        {
            Texture2D tex = m_GltfImport.GetTexture(i);
            if (tex != null){
                VirtualSceneGraph.AssignTextureIndexToTexture(i, tex);
            }
        }

        for (var i = 0; i < m_GltfImport.MaterialCount; i++)
        {
            UnityEngine.Material mat = m_GltfImport.GetMaterial(i);
            if (mat != null){
                VirtualSceneGraph.AssignMaterialIndexToMaterial(i, mat);
            }
        }

#if UNITY_ANIMATION
        AnimationClip[] animationClips = m_GltfImport.GetAnimationClips();
        for (var i = 0; i < animationClips.Length; i++) {
            VirtualSceneGraph.AssignAnimationIndexToAnimationClip(i, animationClips[i]);
        }
#endif

    }

    void ProcessMpegSceneInteractivityExtension(GLTFast.Schema.Scene scene){
        
        //// IDCC
        if (scene.extensions.MPEG_scene_interactivity != null)
        {
            MpegSceneInteractivity mpegSceneInteractivity = scene.extensions.MPEG_scene_interactivity;
            if (mpegSceneInteractivity.triggers != null)
            {
                // Create triggers
                for (int i = 0; i < mpegSceneInteractivity.triggers.Length; i++)
                {
                    GLTFast.Schema.Trigger trig = scene.extensions.MPEG_scene_interactivity.triggers[i];
                    AddMPEGInteractivityTrigger(trig, i);
                }
            }
            if (mpegSceneInteractivity.actions != null)
            {
                // Create actions
                for (int i = 0; i < mpegSceneInteractivity.actions.Length; i++)
                {
                    GLTFast.Schema.Action act = scene.extensions.MPEG_scene_interactivity.actions[i];
                    AddMPEGInteractivityAction(act, i);
                }
            }
            if(mpegSceneInteractivity.behaviors != null)
            {
                // Create behaviors
                for (int i = 0; i < mpegSceneInteractivity.behaviors.Length; i++)
                {
                    GLTFast.Schema.Behavior bhv = scene.extensions.MPEG_scene_interactivity.behaviors[i];
                    AddMPEGInteractivityBehavior(bhv, i);
                }
            }
        }
    }


    void ProcessMpegAnchorExtension(GLTFast.Schema.Scene scene){

        var gltfRoot = m_GltfImport.GetSourceRoot();

        //// IDCC
        if (gltfRoot.Extensions.MPEG_anchor != null &&
            (gltfRoot.Extensions.MPEG_anchor.trackables != null
            || gltfRoot.Extensions.MPEG_anchor.anchors != null))
        {
            Debug.Log("Read gltfRoot.Extensions");
            MpegAnchor anc = gltfRoot.Extensions.MPEG_anchor;
            if(anc.trackables != null)
            {
                // Create trackables
                for(int i = 0; i < anc.trackables.Length; i++)
                {
                    Trackable trackable = gltfRoot.Extensions.MPEG_anchor.trackables[i];
                    AddMPEGTrackables(trackable, i);
                }
            }
            if(anc.anchors != null)
            {
                // Create anchors
                for (int i = 0; i < anc.anchors.Length; i++)
                {
                    GLTFast.Schema.Anchor anch = gltfRoot.Extensions.MPEG_anchor.anchors[i];
                    AddMPEGAnchor(anch, i);
                }
            }
        }


        // Extension at scene level
        // -1 trick to know if the class has been instantiated by the parser
        // Can't be null
        if (scene.extensions.MPEG_anchor != null)
        {
            MpegAnchorObject mpegAnchor = scene.extensions.MPEG_anchor;
            if(mpegAnchor.anchor != -1)
            {
                Debug.Log("Read scene.extensions");
                // Now retrieve index in VirtualSceneGraph
                IMpegAnchor anchor = VirtualSceneGraph.GetAnchorFromIndex(mpegAnchor.anchor);
            
                // Get Anchor to retrieve trackable
                var index = anchor.GetTrackableIndex();
                Debug.Log("index of anchor: "+index);
            
                var track = VirtualSceneGraph.GetTrackableFromIndex(index);
            
                if(track == null)
                    Debug.LogError("Error No trackable");
                else
                    Debug.Log("Type Trackable: "+track.GetType());
                track.Init();
            
                //Now attach all root nodes to anchor.
                uint[]nodes = scene.nodes;
                for(int i = 0; i < nodes.Length;i++)
                {
                    var node = VirtualSceneGraph.GetGameObjectFromIndex((int)nodes[i]);
                    anchor.AttachNodeToAnchor(node);
                }
                anchor.SetUp();     
            }         
        }

        // Anchor detection
        if(VirtualSceneGraph.GetAnchorObjectCount() > 0)
        {
            Debug.Log("Read node.Extensions");
            foreach(int key in VirtualSceneGraph.GetAnchorObjectKeys())
            {
                MpegAnchorObject anc = VirtualSceneGraph.GetAnchorObjectFromIndex(key);
                IMpegAnchor anchor = VirtualSceneGraph.GetAnchorFromIndex(anc.anchor);
                // Get Anchor to retrieve trackable
                var index = anchor.GetTrackableIndex();
                Debug.Log("Index of Anchor in array: "+index);
                var track = VirtualSceneGraph.GetTrackableFromIndex(index);
                
                if(track == null)
                    Debug.LogError("No trackable");
                else
                    Debug.Log(" Trackable Type: "+track.GetType());
                track.Init();
                var nodeGO = VirtualSceneGraph.GetGameObjectFromIndex(key);
                anchor.AttachNodeToAnchor(nodeGO);
                anchor.SetUp();
            }
        }
    }


    void OnMeshAdded(
            GameObject gameObject,
            uint nodeIndex,
            string meshName,
            MeshResult meshResult,
            uint[] joints = null,
            uint? rootJoint = null,
            float[] morphTargetWeights = null,
            int meshNumeration = 0
    ){
        //// IDCC
        VirtualSceneGraph.AssignMeshToMeshIndex(meshResult.meshIndex, meshResult.mesh);
    }


    public void AddAudioSources(GameObject go, GLTFast.Schema.NodeBase aNode)
    {
        var root = m_GltfImport.GetSourceRoot();
        foreach (var srcDef in aNode.Extensions.MPEG_audio_spatial.sources)
        {
            var aSrc = go.AddComponent<SpatialAudioSource>() as SpatialAudioSource;
            int bufferId = root.bufferViews[root.accessors[srcDef.accessors[0]].bufferView].buffer;
            aSrc.Configure(srcDef, bufferId);
            m_Instantiator.SceneInstance.AddAudioSource(aSrc);
        }
    }


    public void AddAudioListener(GameObject go)
    {
        var aLstn = go.AddComponent(typeof(AudioListener)) as AudioListener;
        // doesn't check if multiple listeners are configured/enabled
        aLstn.enabled = true;
        aLstn.velocityUpdateMode = AudioVelocityUpdateMode.Auto;
        m_Instantiator.SceneInstance.SetAudioListener(aLstn);
    }


    //// IDCC
    public void AddMPEGInteractivityBehavior(GLTFast.Schema.Behavior bhv, int index)
    {
        GameObject go = new GameObject($"Behavior - {index}");
        // Not useful to have an interface here, but following
        // the same pattern than actions and triggers
        IMpegInteractivityBehavior bhvIf = go.AddComponent<GLTFast.Behavior>();
        bhvIf.InitializeBehavior(bhv);
        m_Instantiator.SceneInstance.behaviorController.AddBehavior(bhvIf);
        VirtualSceneGraph.AssignBehaviorIndexToBehavior(bhvIf, index);
    }


    //// IDCC
    public void AddMPEGInteractivityTrigger(GLTFast.Schema.Trigger trigger, int index)
    {
        GameObject go = new GameObject($"{trigger.type} - {index}");
        IMpegInteractivityTrigger triggerIf = null;
        switch (trigger.type)
        {
            case TriggerType.TRIGGER_COLLISION: triggerIf = go.AddComponent<CollisionSceneTrigger>(); break;
            case TriggerType.TRIGGER_PROXIMITY: triggerIf = go.AddComponent<ProximitySceneTrigger>(); break;
            case TriggerType.TRIGGER_USER_INPUT: triggerIf = go.AddComponent<UserInputSceneTrigger>(); break;
            case TriggerType.TRIGGER_VISIBILITY: triggerIf = go.AddComponent<VisibilitySceneTrigger>(); break;
        }
        if (triggerIf == null)
        {
            throw new NotImplementedException($"Couldn't create trigger, type not recognized: {trigger.type}");
        }
        triggerIf.Init(trigger);
        VirtualSceneGraph.AssignTriggerToIndex(triggerIf, index);
        m_Instantiator.SceneInstance.AddInteractivityTrigger(triggerIf);
    }


    //// IDCC
    public void AddMPEGInteractivityAction(GLTFast.Schema.Action action, int index)
    {
        GameObject go = new GameObject($"{action.type} - {index}");
        IMpegInteractivityAction actionIf = null;
        switch (action.type)
        {
            case ActionType.ACTION_ACTIVATE: actionIf = go.AddComponent<ActionActivate>(); break;
            case ActionType.ACTION_TRANSFORM: actionIf = go.AddComponent<ActionTransform>(); break;
            case ActionType.ACTION_BLOCK: actionIf = go.AddComponent<ActionBlock>(); break;
            case ActionType.ACTION_ANIMATION: actionIf = go.AddComponent<ActionAnimation>(); break;
            case ActionType.ACTION_MEDIA: actionIf = go.AddComponent<ActionMedia>(); break;
            case ActionType.ACTION_MANIPULATE: actionIf = go.AddComponent<ActionManipulate>(); break;
            case ActionType.ACTION_SET_MATERIAL: actionIf = go.AddComponent<ActionSetMaterial>(); break;
            case ActionType.ACTION_SET_HAPTIC: actionIf = go.AddComponent<ActionSetHaptic>(); break;
            case ActionType.ACTION_SET_AVATAR: actionIf = go.AddComponent<ActionSetAvatar>(); break;
        }
        if (actionIf == null)
        {
            throw new NotImplementedException($"Couldn't create action, type not recognized: {action.type} : {index}");
        }
        actionIf.Init(action);
        VirtualSceneGraph.AssignActionToIndex(actionIf, index);
        m_Instantiator.SceneInstance.AddInteractivityAction(actionIf);
    }


    //// IDCC
    public void AddMPEGTrackables(GLTFast.Schema.Trackable trackable, int index) {
#if UNITY_ANDROID
        GameObject go = new GameObject($"{trackable.type} - {index}");
        IMpegTrackable trackIf = null;
        // Debug.Log("Tracking Mode: " + trackable.type);
        switch(trackable.type)
        {
            case TrackableType.TRACKABLE_FLOOR:     trackIf = go.AddComponent<TrackableFloor>(); break;
            case TrackableType.TRACKABLE_VIEWER:     trackIf = go.AddComponent<TrackableViewer>(); break;
            case TrackableType.TRACKABLE_CONTROLLER:    trackIf = go.AddComponent<TrackableController>(); break;
            case TrackableType.TRACKABLE_PLANE:    trackIf = go.AddComponent<TrackableGeometric>(); break;
            case TrackableType.TRACKABLE_MARKER_2D:    trackIf = go.AddComponent<TrackableMarker2D>(); break;
            case TrackableType.TRACKABLE_MARKER_3D:    trackIf = go.AddComponent<TrackableMarker3D>(); break;
            case TrackableType.TRACKABLE_MARKER_GEO:    trackIf = go.AddComponent<TrackableMarkerGeo>(); break;
            case TrackableType.TRACKABLE_APPLICATION:    trackIf = go.AddComponent<TrackableApplication>(); break;
        }
        if (trackIf == null)
        {
            throw new NotImplementedException($"Couldn't create trackable, type not recognized: {trackable.type}");
        }
        trackIf.InitFromGltf(trackable);
        VirtualSceneGraph.AssignTrackableToIndex(trackIf, index);
        m_Instantiator.SceneInstance.AddTrackable(trackIf);
#endif
    }


    //// IDCC
    public void AddMPEGAnchor(GLTFast.Schema.Anchor anchor, int index) {
#if UNITY_ANDROID
        GameObject go = new GameObject($"anchor - {index}");
        IMpegAnchor anchIf = null;
        anchIf = go.AddComponent<AnchorInstance>();
        if(anchIf == null)
        {
            throw new NotImplementedException($"Couldn't create anchor");
        }
        anchIf.Init(anchor);
        VirtualSceneGraph.AssignAnchorToIndex(anchIf, index);
        m_Instantiator.SceneInstance.AddAnchor(anchIf);
#endif
    }


    public void Dispose(){
        m_Instantiator.SceneInstance.DestroyInstance();
    }

}
