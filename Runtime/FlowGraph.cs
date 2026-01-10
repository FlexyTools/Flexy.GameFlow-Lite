namespace Flexy.GameFlow;

public class FlowGraph
{
	public FlowGraph	( Service_GameFlow service )
	{
		_service		= service;
		var rootState	= service;
		
		_root = new FlowNode
		{
			Graph		= this,
			State		= rootState, 
			IsShowed	= true,
		};
		
		_root.SpawnTransitionRoot();
		
		rootState._node = _root;
		rootState.DoShow();
	}

	private		Service_GameFlow _service;
	private		FlowNode		 _root;
	
	public		Service_GameFlow Service	=> _service;
	public		FlowNode		 Root		=> _root;

	public		FlowNode		Open		( AssetRef<GameStage> stageRef, Object? openParams = null, GameContext? parentContext = null, Scene spawnIn = default )	
	{
		var stagePrefab = stageRef.LoadAssetSync();
		
		if (stagePrefab == null)
			throw new ArgumentException("[FlowGraph] stageRef is invalid", nameof(stageRef));
		
		stagePrefab._prefabRef = new AssetRef<State>(stageRef.Uid, stageRef.SubId);
	
		var activeSelf = stagePrefab.gameObject.activeSelf;
		stagePrefab.gameObject.SetActive(false);
		
		var stage = (GameStage)UObject.Instantiate( stagePrefab, spawnIn.IsValid() ? spawnIn : Service.gameObject.scene );
		stage._prefabRef = stagePrefab._prefabRef;
		stage._graph = this;
		stage._owner = _service;
		
		stage.transform.SetSiblingIndex(0);
		stage.NicifyName();
		
		stagePrefab.gameObject.SetActive( activeSelf );
		stagePrefab.gameObject.ClearEditorDirty();
	
		var newNode	= SpawnNode(stage, openParams, _root);
		
		stage.PreInit(parentContext);
		
		return newNode;
	}
	public		FlowNode		Open		( AssetRef<State> stateRef, FlowNode callSource, Object? openParams = null, FlowNode? parent = null )					
	{
		var state = default(State);
		
		if (callSource.GameStageNode is {IsOpened:true})
		{
			var instances = ((GameStage)callSource.GameStageNode.State)._statesCache;
			instances.TryGetValue(stateRef, out state);
		}
		
		var stateInstanceOrPrefab = state;
		
		if (!stateInstanceOrPrefab)
		{
			stateInstanceOrPrefab = stateRef.LoadAssetSync();
			
			if (!stateInstanceOrPrefab)
				throw new ArgumentException("[FlowGraph] stateRef is invalid", nameof(stateRef));
			
			stateInstanceOrPrefab!._prefabRef = stateRef;
		}
	
		if (stateInstanceOrPrefab is GameStage)
			return Open(new AssetRef<GameStage>(stateRef.Uid, stateRef.SubId), openParams);
	
		parent ??= callSource.GameStageNode is {IsOpened:true} stage ? stage : _root.FirstChild!.GetLastSibling();
		
		// If we have main substate
		if (!parent.MainSubStateRef.IsNone)
		{
			var isOpeningMainState = stateRef == parent.MainSubStateRef; 
			
			if (!isOpeningMainState && parent.FirstChild == null)
			{
				// In case main state not spawned and we try to open not main state => Open main substate first
				Open(parent.MainSubStateRef, callSource, null, parent);
			}
			else if (isOpeningMainState && parent.FirstChild != null)
			{
				// In case main state exists just Close all states up to main 
				RemoveNodesUpTo( parent.FirstChild.GetLastSibling(), parent.FirstChild, openParams );
				return parent.FirstChild;
			}
		}		
		
		if (state == null)
			state = parent.State.InstantiateState(stateInstanceOrPrefab!, "Base"); 
	
		var newNode = SpawnNode(state, openParams, parent);
		
		return newNode;
	}
	public		FlowNode		TryGoBack	( )																														
	{
		return _root.TransitionHost.TryGoBack();
	}

	internal	void			RemoveNode		( FlowNode node )												
	{
		if (node.Back != null)
			RemoveNodesUpTo( node, node.Back );
	}
	internal	void			RemoveNodesUpTo	( FlowNode? source, FlowNode target, Object? openParams = null )
	{
		if (source is not {IsOpened:true} || source == _root)
			return;
		
		var iter	= source;
		var trn		= source.TransitionHost;

		while (iter != null && iter != target)
		{
			if (iter == _root) 
				break;

			var toRemove	= iter;
			iter			= toRemove.Back;
			
			while (toRemove.FirstChild != null)
				RemoveNodesUpTo( toRemove.FirstChild.GetLastSibling(), toRemove );
			
			if (toRemove.Back != null)		toRemove.Back.Forward = toRemove.Forward;
			if (toRemove.Forward != null)	toRemove.Forward.Back = toRemove.Back!;
			
			if (toRemove.Parent.FirstChild == toRemove)
				toRemove.Parent.FirstChild = toRemove.NextSibling;
			
			if (toRemove.PrevSibling != null) toRemove.PrevSibling.NextSibling = toRemove.NextSibling;
			if (toRemove.NextSibling != null) toRemove.NextSibling.PrevSibling = toRemove.PrevSibling;
			
			if (toRemove == trn._tipNode)
				trn._tipNode  = iter!;
		}

		if (openParams != null && iter == target)
			iter!.OpenParams = openParams;

		trn.ScheduleSwitchStates();
	}
	internal	void			DestroyState	( State state )													
	{
		if (state._node == _root)
			return;
		
		state._owner!.DestroySubState(state);
	}
	
	private		FlowNode		SpawnNode		( State state, Object? openParams, FlowNode parent )			
	{
		var node = new FlowNode
		{
			Graph			= this,
			StateRef		= state.PrefabRef,
			MainSubStateRef	= state.MainSubStateRef,
			State			= state,
			OpenParams		= openParams, 
			Parent			= parent,	
		};

		node.PrevSibling 	= parent.FirstChild.GetLastSiblingOrNull();

		if (node.PrevSibling != null)
			node.PrevSibling.NextSibling = node;
		
		if (parent.FirstChild == null)
			parent.FirstChild = node;
		
		var tr = parent.TransitionHost;
		tr._tipNode.Forward = node;
		node.Back = tr._tipNode;
		
		tr._tipNode = node;
		
		tr.ScheduleSwitchStates();
		
		return node;
	}
	
#if UNITY_EDITOR
	public void DrawRuntimeUI( )
	{
		if (!Application.isPlaying || !_root.State)
			return;
		
		// Draw root
		GUILayout.Space( 16 );
		GUILayout.Label( "Flow Graph" );
		GUILayout.Space( 5 );
		
		GUILayout.BeginHorizontal();
		{
			GUILayout.Label( $"{(_root.IsShowing ? "■" : "□")}", GUILayout.Width(20) );
			
			if (GUILayout.Button($"{_root.State.name}", GUI.skin.label))
				UnityEditor.EditorGUIUtility.PingObject(_root.State);
			
			GUILayout.FlexibleSpace();
			if (GUILayout.Button( "?" ))
				UnityEditor.EditorGUIUtility.PingObject(_root.State);
		}
		GUILayout.EndHorizontal();
		GUILayout.Space( 5 );
		
		for (var node = _root.Forward; node != null; node = node.Forward)
		{
			if (node.State is GameStage)
				GUILayout.Space( 5 );
				
			GUILayout.BeginHorizontal();
			GUILayout.Space( node.State is GameStage ? 10 : 26 );
			GUILayout.Label( $"{(node.IsShowing ? "■" : "□")}", GUILayout.Width(20) );
			if (GUILayout.Button($"{node.State.name} {(node.OpenParams != null ? "op:" + node.OpenParams : "")}", GUI.skin.label))
				UnityEditor.EditorGUIUtility.PingObject(node.State);
			
			GUILayout.FlexibleSpace();
			if (GUILayout.Button( "?" ))
				UnityEditor.EditorGUIUtility.PingObject(node.State);
			
			GUILayout.EndHorizontal();
		}
	}
#endif
}