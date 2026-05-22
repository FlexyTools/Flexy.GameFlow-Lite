namespace Flexy.GameFlow;

public class FlowGraph
{
	public FlowGraph	( Service_GameFlow gameFlow )
	{
		_flow			= gameFlow;
		var rootState	= gameFlow;
		
		_root = new FlowNode
		{
			Graph		= this,
			State		= rootState, 
		};
		
		_root.SpawnTransitionRoot();
		
		rootState._node = _root;
		rootState.DoShow().Forget();
	}

	private		Service_GameFlow _flow;
	private		FlowNode		 _root;
	
	public		Service_GameFlow Flow		=> _flow;
	public		FlowNode		 Root		=> _root;

	public		FlowNode		Open		( AssetRef<GameStage> stageRef, object? openParams = null, GameContext? parentContext = null, Scene spawnIn = default )	
	{
		var prefabPref	= new AssetRef<State>(stageRef.Uid, stageRef.SubId);
		var instances	= ((GameStage)_root.GameStageNode.State)._statesCache;
		instances.TryGetValue(prefabPref, out var state);

		var stage		= (GameStage?)state;
		var isNewState	= stage == null;
	
		if (stage == null)
		{
			var stagePrefab = stageRef.LoadAssetSync();
			
			if (stagePrefab == null)
				throw new ArgumentException("[FlowGraph] stageRef is invalid", nameof(stageRef));
			
			stagePrefab._prefabRef = new AssetRef<State>(stageRef.Uid, stageRef.SubId);
		
			var activeSelf = stagePrefab.gameObject.activeSelf;
			stagePrefab.gameObject.SetActive(false);
			
			stage = (GameStage)Object.Instantiate( stagePrefab, spawnIn.IsValid() ? spawnIn : Flow.gameObject.scene );
			stage._prefabRef = stagePrefab._prefabRef;
			stage._graph = this;
			stage._owner = _flow;
			
			stage.transform.SetSiblingIndex(0);
			stage.NicifyName();
			
			stagePrefab.gameObject.SetActive( activeSelf );
			stagePrefab.gameObject.ClearEditorDirty();
			
			instances.Add(prefabPref, stage);
		}

		Debug.Log( "Spawned", stage);

		var stageNode	= SpawnNode(stage, openParams, _root);
		
		if (isNewState)
		{		
			stage._node				= stageNode;
			stage.PreInitContext	(parentContext);
		}
		
		return stageNode;
	}
	public		FlowNode		Open		( AssetRef<State> stateRef, FlowNode callSource, object? openParams = null )											
	{
		var stateType = Flow.GetRefType(stateRef);
		
		if (typeof(GameStage).IsAssignableFrom(stateType))
			return Open(new AssetRef<GameStage>(stateRef.Uid, stateRef.SubId), openParams);
	
		var parent = callSource.GameStageNode is {IsOpened:true} stage ? stage : _root.FirstBaseChild!.GetLastSibling();
		
		// If we have main substate
		if (!parent.MainSubStateRef.IsNone)
		{
			var isOpeningMainState = stateRef == parent.MainSubStateRef; 
			
			if (!isOpeningMainState && parent.FirstBaseChild == null)
			{
				// In case main state not spawned and we try to open not main state => Open main substate first
				Open(parent.MainSubStateRef, callSource);
			}
			else if (isOpeningMainState && parent.FirstBaseChild != null)
			{
				// In case main state exists just Close all states up to main 
				RemoveNodesUpTo( parent.FirstBaseChild.GetLastSibling(), parent.FirstBaseChild, openParams );
				return parent.FirstBaseChild;
			}
		}		
		
		var instances = ((GameStage)parent.State)._statesCache;
		instances.TryGetValue(stateRef, out var state);

		var isNewState	= state == null;

		if (state == null)
		{
			var prefab = stateRef.LoadAssetSync()!;
			state = InstantiateState((GameStage)parent.State, prefab, stateRef);
		} 
			
		var stateNode = SpawnNode(state, openParams, parent);

		if (isNewState)
			state._node	= stateNode; 
		
		return stateNode;
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
	internal	void			RemoveNodesUpTo	( FlowNode? source, FlowNode target, object? openParams = null )
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
			
			while (toRemove.FirstBaseChild != null)
				RemoveNodesUpTo( toRemove.FirstBaseChild.GetLastSibling(), toRemove );
			
			if (toRemove.Back != null)		toRemove.Back.Forward = toRemove.Forward;
			if (toRemove.Forward != null)	toRemove.Forward.Back = toRemove.Back!;
			
			if (toRemove.Parent.FirstBaseChild == toRemove)
				toRemove.Parent.FirstBaseChild = toRemove.NextSibling;
			
			if (toRemove.PrevSibling != null) toRemove.PrevSibling.NextSibling = toRemove.NextSibling;
			if (toRemove.NextSibling != null) toRemove.NextSibling.PrevSibling = toRemove.PrevSibling;
			
			if (toRemove == trn._tipNode)
				trn._tipNode  = iter!;
		}

		if (openParams != null && iter == target)
			iter!.OpenParams = openParams;

		trn.ScheduleSwitchStates();
	}
	
	internal 	State			InstantiateState( GameStage parent, State statePrefab, AssetRef<State> prefabRef )	
	{
		statePrefab._prefabRef = prefabRef;
		var active = statePrefab.gameObject.activeSelf;
		statePrefab.gameObject.SetActive(false);
		var state = parent.InstantiateSubState(statePrefab, "Base");
		statePrefab.gameObject.SetActive(active);
		statePrefab.gameObject.ClearEditorDirty();
			
		state._prefabRef = prefabRef;
		state._owner = parent;
		state._graph = parent._graph;
			
		state.NicifyName();
			
		parent._statesCache.Add(prefabRef, state);
			
		return state;
	}
	internal	void			DestroyState	( State state )													
	{
		if (state._node == _root)
			return;
			
		state._owner.GameStage._statesCache.Remove(state.PrefabRef);
		((GameStage)state._owner).DestroySubState(state);
	}
	
	private		FlowNode		SpawnNode		( State state, object? openParams, FlowNode parent )			
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

		node.PrevSibling 	= parent.FirstBaseChild.GetLastSiblingOrNull();
		
		if (node.PrevSibling != null)
			node.PrevSibling.NextSibling = node;
		
		if (parent.FirstBaseChild == null)
			parent.FirstBaseChild = node;
		
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
			if (GUILayout.Button($"{node.State.name} {(node.OpenParams != null ? $"({node.OpenParams})" : "")}", GUI.skin.label))
				UnityEditor.EditorGUIUtility.PingObject(node.State);
			
			GUILayout.FlexibleSpace();
			if (GUILayout.Button( "?" ))
				UnityEditor.EditorGUIUtility.PingObject(node.State);
			
			GUILayout.EndHorizontal();
		}
	}
#endif
}