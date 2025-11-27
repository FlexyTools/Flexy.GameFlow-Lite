namespace Flexy.GameFlow;

public class FlowGraph
{
	public FlowGraph	( Service_GameFlow service, AssetRef<FlowRoot> rootStateRef )
	{
		_service = service;
		
		// Spawn Root State and node
		{
			State rootState;
		
			if (!rootStateRef.IsNone)
			{
				var rootPrefab		= rootStateRef.LoadAssetSync();
				
				if (!rootPrefab)
					throw new ArgumentException("[FlowGraph] rootStateRef is invalid", nameof(rootStateRef));
				
				var activeSelf		= rootPrefab!.gameObject.activeSelf;
				rootPrefab.gameObject.SetActive( false );
				rootState = UObject.Instantiate( rootPrefab, service.transform );
				rootPrefab.gameObject.SetActive(activeSelf);
				rootPrefab.gameObject.ClearEditorDirty();
			}
			else
			{
				var go = new GameObject("FlowRoot");
				go.SetActive(false);
				go.transform.parent = service.transform;
				rootState = go.AddComponent<FlowRoot>(); 
			}
		
			rootState.name			= "[Root] " + rootState.name.Replace( "(Clone)", "" ).Replace("_", " ").Trim('_');
			rootState._prefabRef	= new (rootStateRef.Uid, rootStateRef.SubId);
			rootState._graph		= this;
		
			_root = new FlowNode
			{
				Graph		= this,
				State		= rootState, 
				FullyInited	= true
			};

			_mainLineActive	= _root;
			_mainLineTip	= _root;
			
			rootState._node = _root;
			rootState.gameObject.SetActive( true );
			rootState.DoShow();
		}
		
		SwitchStatesAsyncInfiniteLoop().Forget();
	}

	private readonly	Dictionary<AssetRef<State>, State>	_globalStateInstances	= new(32);

	private		Service_GameFlow _service;
	private		FlowNode		_root;
	private		FlowNode		_mainLineTip;
	internal	FlowNode		_mainLineActive;
	private		Boolean			_doTransition;

	public		Service_GameFlow Service		=> _service;
	public		FlowNode		 Root			=> _root;

	public		FlowNode		MainLineTip		=> _mainLineTip;
	public		FlowNode		MainLineActive	=> _mainLineActive;
	
	public		StateHandle		Open	( AssetRef<GameStage> stageRef, GameContext? parentContext = null, Object? openParams = null, Scene spawnIn = default )	
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
		
		stage.transform.SetSiblingIndex(0);
		stage.NicifyName();
		
		stagePrefab.gameObject.SetActive( activeSelf );
		stagePrefab.gameObject.ClearEditorDirty();
	
		var newNode	= SpawnNode(stage, openParams, _root);
		
		stage.Init(parentContext);
		
		return newNode.Handle;
	}
	public		StateHandle		Open	( AssetRef<State> stateRef, State? callSource, Object? openParams = null, FlowNode? parent = null )						
	{
		var state = default(State);
		
		if (callSource != null && callSource.GameStage._node is {IsValid:true})
		{
			var instances = callSource.GameStage._stateInstances;
			instances.TryGetValue(stateRef, out state);
		}

		if (!state)
			_globalStateInstances.TryGetValue( stateRef, out state );
		
		var stateInstanceOrPrefab = state;
		
		if (!stateInstanceOrPrefab)
		{
			stateInstanceOrPrefab = stateRef.LoadAssetSync();
			
			if (!stateInstanceOrPrefab)
				throw new ArgumentException("[FlowGraph] stateRef is invalid", nameof(stateRef));
			
			stateInstanceOrPrefab!._prefabRef = stateRef;
		}
	
		if (stateInstanceOrPrefab is GameStage)
			return Open(new AssetRef<GameStage>(stateRef.Uid, stateRef.SubId), null, openParams);
	
		if (parent == null)
		{
			if (callSource)
				parent = callSource!.GameStage._node is {IsValid:true} stage ? stage : null;
			
			parent ??= _root.FirstChild!.GetLastSibling();
		}
		
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
				return parent.FirstChild.Handle;
			}
		}		
		
		if (!state)
			state = parent.State.InstantiateState(stateInstanceOrPrefab!); 
	
		var newNode = SpawnNode(state!, openParams, parent);
		
		return newNode.Handle;
	}
	public		StateHandle		GoBack	( )																														
	{
		if (_mainLineTip.Back != null && _mainLineTip.State.TryGoBack())
			RemoveNodesUpTo(_mainLineTip, _mainLineTip.Back);

		return _mainLineTip.Handle;
	}

	internal	void			RemoveNode		( FlowNode node )																				
	{
		if (node.Back != null)
			RemoveNodesUpTo( node, node.Back );
	}
	internal	void			RemoveNodesUpTo	( FlowNode source, FlowNode target, Object? openParams = null, Boolean skipCurrent = false )	
	{
		if (source is not { IsValid: true } || source == _root)
			return;
		
		var iter	= source;

		if (skipCurrent)
			iter = iter.Back;

		while (iter != null && iter != target)
		{
			if (iter == _root) 
				break;

			var toRemove	= iter;
			iter			= iter.Back;
			
			if (toRemove.FirstChild != null)
				RemoveNodesUpTo( toRemove.FirstChild.GetLastSibling(), toRemove );
			
			if (toRemove == _mainLineTip)
				_mainLineTip  = iter!;
			
			if (toRemove.Back != null)		toRemove.Back.Forward = toRemove.Forward;
			if (toRemove.Forward != null)	toRemove.Forward.Back = toRemove.Back!;
			
			if (toRemove.Parent.FirstChild == toRemove)
				toRemove.Parent.FirstChild = toRemove.NextSibling;
			
			if (toRemove.PrevSibling != null) toRemove.PrevSibling.NextSibling = toRemove.NextSibling;
			if (toRemove.NextSibling != null) toRemove.NextSibling.PrevSibling = toRemove.PrevSibling;
		}

		if (iter == null) 
			return;

		if (openParams != null)
			iter.OpenParams = openParams;

		ScheduleSwitchStates();
	}
	internal	void			DestroyInstance ( State instance )																				
	{
		if (instance._owner != null)
		{
			instance._owner.DestroySubState(instance);
		}
		else
		{
			_globalStateInstances.Remove(instance.PrefabRef);
			UObject.Destroy(instance.gameObject);
		}
	}
	
	internal		void		TransitionNow					( )		
	{
		if (!_doTransition) 
			return;
		
		_doTransition = false;
		DoStateTransitions();
	}
	private			void		ScheduleSwitchStates			( )		
	{
		_doTransition	= true;
	}
	private async	UniTask		SwitchStatesAsyncInfiniteLoop	( )		
	{
		while (Application.isPlaying && _root.State)
		{
			// Change view at last update (before animations)
			// This will allow to make many changes in update and then only one view transition
			await UniTask.NextFrame( PlayerLoopTiming.LastUpdate );

			if (!_doTransition)
				continue;

			_doTransition	= false;

			try						{ DoStateTransitions(); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
		}
	}
	private 		void		DoStateTransitions				( )		
	{
		TransitionOperationBasis.InstantTransition( _mainLineActive, _mainLineTip );
	}
	
	private			FlowNode	SpawnNode						( State state, Object? openParams, FlowNode parent )	
	{
		var nextNode = new FlowNode
		{
			Graph			= this,
			StateRef		= state.PrefabRef,
			MainSubStateRef	= state.MainSubStateRef,
			State			= state,
			OpenParams		= openParams, 
			PrevSibling 	= parent.FirstChild.GetLastSiblingOrNull()
		};

		if (nextNode.PrevSibling != null)
			nextNode.PrevSibling.NextSibling = nextNode;
		
		nextNode.Parent = parent;
		
		if (parent.FirstChild == null)
			parent.FirstChild = nextNode;
		
		_mainLineTip.Forward = nextNode;
		nextNode.Back = _mainLineTip;
		
		_mainLineTip = nextNode;
		
		ScheduleSwitchStates();
		
		return nextNode;
	}
	
#if UNITY_EDITOR
	public void DrawRuntimeUI( )
	{
		if (!Application.isPlaying || !_root.State)
			return;
		
		// Draw root
		GUILayout.Space( 10 );
		GUILayout.BeginHorizontal();
		{
			GUILayout.Label( $"{(_root.IsShowed ? "■" : "□")}", GUILayout.Width(20) );
			GUILayout.Label( $"{_root.State.name}" );
		}
		GUILayout.EndHorizontal();
		GUILayout.Space( 5 );
		
		for (var node = _root.Forward; node != null; node = node.Forward)
		{
			if (node.State is GameStage)
				GUILayout.Space( 5 );
				
			GUILayout.BeginHorizontal();
			GUILayout.Space( node.State is GameStage ? 10 : 26 );
			GUILayout.Label( $"{(node.IsShowed ? "■" : "□")}", GUILayout.Width(20) );
			GUILayout.Label( $"{node.State.name} {(node.OpenParams != null ? "op:" + node.OpenParams : "")}" );
			GUILayout.EndHorizontal();
		}
	}
#endif
}