namespace Flexy.GameFlow;

public class FlowGraph
{
	public FlowGraph	( Service_GameFlow service, AssetRef<State> rootStateRef )
	{
		_service = service;
		
		// Spawn Root State and node
		{
			var rootPrefab		= rootStateRef.LoadAssetSync();
			
			if (!rootPrefab)
				throw new ArgumentException("[FlowGraph] rootStateRef is invalid", nameof(rootStateRef));
			
			var activeSelf		= rootPrefab!.gameObject.activeSelf;
			rootPrefab.gameObject.SetActive( false );
			var state = UnityEngine.Object.Instantiate( rootPrefab, service.transform );
			rootPrefab.gameObject.SetActive(activeSelf);
			rootPrefab.gameObject.ClearEditorDirty();
		
			state.name			= "[Root] " + state.name.Replace( "(Clone)", "" ).Replace("_", " ").Trim('_');
			state._prefabRef	= rootStateRef;
			state._graph		= this;
		
			_root = new FlowNode
			{
				Graph		= this,
				State		= state, 
				IsLocked	= true,
				FullyInited	= true
			};

			_mainLineActive	= _root;
			_mainLineTip	= _root;
			
			state._node = _root;
			state.gameObject.SetActive( true );
			state.DoShow();
		}
		
		SwitchStatesAsyncInfiniteLoop().Forget();
	}

	private readonly	Dictionary<AssetRef<State>, State>	_globalStateInstances	= new(32);

	private		Service_GameFlow _service;
	private		FlowNode		_root;
	private		FlowNode		_mainLineTip;
	private		FlowNode		_mainLineActive;
	private		Boolean			_doTransition;

	public		Service_GameFlow Service		=> _service;
	public		FlowNode		 Root			=> _root;

	public		FlowNode		MainLineTip		=> _mainLineTip;
	public		FlowNode		MainLineActive	=> _mainLineActive;
	
	public		StateHandle		Open	( AssetRef<GameStage> stageRef, GameContext? parentContext = null, Object? openParams = null, Scene spawnIn = default )																		
	{
		return Open( new AssetRef<State>(stageRef.Uid, stageRef.SubId), null, openParams, null, true, spawnIn, parentContext );	
	}
	public		StateHandle		Open	( AssetRef<State> stateRef, State? callSource, Object? openParams = null, FlowNode? parent = null, Boolean isLocked = false, Scene spawnIn = default, GameContext? parentContext = null )	
	{
		var newNode			= SpawnStateAndNode( stateRef, openParams, callSource, parent, isLocked, spawnIn );
		
		if (newNode.State is GameStage gs)
			gs.Init( parentContext );
		
		return newNode.Handle;
	}

	internal	StateHandle		GoBack			( )																								
	{
		if (_mainLineTip != _root && _mainLineTip.Back != null && (_mainLineTip.State?.TryGoBack() ?? false))
			RemoveNodesUpTo( _mainLineTip, _mainLineTip.Back );

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
	
	public		void			TransitionNow		( )																														
	{
		if (!_doTransition) 
			return;
		
		_doTransition = false;
		DoStateTransitions();
	}
	private		FlowNode		SpawnStateAndNode	( AssetRef<State> stateRef, Object? openParams, State? callSource, FlowNode? parent, Boolean isLocked, Scene spawnIn )	
	{
		var state = default(State);
		var instances = callSource?.GameStage._stateInstances;
		instances?.TryGetValue( stateRef, out state );

		if (!state)
			_globalStateInstances.TryGetValue( stateRef, out state );
		
		var stateInstanceOrPrefab = state;
		
		if (!stateInstanceOrPrefab)
			stateInstanceOrPrefab = stateRef.LoadAssetSync();
		
		if (!stateInstanceOrPrefab)
			throw new ArgumentException("[FlowGraph] stateRef is invalid", nameof(stateRef));
		
		if (stateInstanceOrPrefab is GameStage _)
		{
			instances	= _globalStateInstances;
			parent		= _root;
			isLocked	= true;
			
			if (!state)
			{
				var statePrefab	= stateInstanceOrPrefab;
				var activeSelf	= statePrefab.gameObject.activeSelf;
				statePrefab.gameObject.SetActive( false );
				
				state = (State)UnityEngine.Object.Instantiate( statePrefab, spawnIn.IsValid() ? spawnIn : Service.gameObject.scene );
				state.transform.SetSiblingIndex(0);
				NicifyStateName(state);
				
				statePrefab.gameObject.SetActive( activeSelf );
				statePrefab.gameObject.ClearEditorDirty();
			}
		}
		else
		{
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
				if (isOpeningMainState)
					isLocked = true;
			
				if (!isOpeningMainState && parent.FirstChild == null)
				{
					// In case main state not spawned and we try to open not main state => Open main substate first
					SpawnStateAndNode(parent.MainSubStateRef, null, callSource, parent, true, spawnIn);
				}
				else if (isOpeningMainState && parent.FirstChild != null)
				{
					// In case main state exists just Close all states up to main 
					RemoveNodesUpTo( parent.FirstChild.GetLastSibling(), parent.FirstChild, openParams );
					return parent.FirstChild;
				}
			}		
			
			if (!state)
			{
				var statePrefab = stateInstanceOrPrefab;
				var activeSelf	= statePrefab!.gameObject.activeSelf;
				statePrefab.gameObject.SetActive(false);
				
				state = parent.State.InstantiateSubState(statePrefab);
				NicifyStateName(state);
				
				statePrefab.gameObject.SetActive(activeSelf);
				statePrefab.gameObject.ClearEditorDirty();
			}
			
			if (instances == null)
				instances = ((GameStage)parent.GameStageNode.State)._stateInstances;
		}

		instances[stateRef] = state!;
		state!._prefabRef = stateRef;
		state._graph = this;
		
		var nextNode = new FlowNode
		{
			Graph			= this,
			StateRef		= stateRef,
			MainSubStateRef	= state.MainSubStateRef,
			State			= state,
			OpenParams		= openParams, 
			IsLocked		= isLocked,
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
		
		static void NicifyStateName(State state)
		{
			try
			{
				var niceName = state.name.Replace( "(Clone)", "" ).Replace("_", " ").Trim('_').Trim(' ');
				var spaceIndex = niceName.IndexOf(' ');
				if (spaceIndex != -1 && spaceIndex < niceName.Length - 1)
					state.name = niceName.Insert(spaceIndex, "]").Insert(0, "[");
			}
			catch (Exception ex) { Debug.LogException(ex); }
		}
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
		TransitionOperationBasis.DoStateTransitions( ref _mainLineActive, _mainLineTip );
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