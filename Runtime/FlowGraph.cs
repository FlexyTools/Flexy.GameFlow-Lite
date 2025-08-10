namespace Flexy.GameFlow;

public class FlowGraph
{
	public FlowGraph	( Service_GameFlow service, AssetRef<State> rootStateRef )
	{
		_service = service;
		_uidNext = 0;
		
		// Spawn Root State and node
		{
			var statePrefab		= rootStateRef.LoadAssetSync();
			var activeSelf		= statePrefab.gameObject.activeSelf;
			statePrefab.gameObject.SetActive( false );
		
			var state = UnityEngine.Object.Instantiate( statePrefab, service.transform );
		
			statePrefab.gameObject.SetActive( activeSelf );
		
			state.name = "[GS] " + state.name.Replace( "(Clone)", "" );
			state._prefabRef = rootStateRef;
			state._graph = this;
		
			_root = new FlowNode
			{
				Graph		= this,
				Uid			= _uidNext++,
				State		= state, 
				OpenParams	= null, 
				IsLocked	= true,
				WasShown	= true
			};

			_mainLineActive = _root;
			_mainLineTip = _root;
			
			state.gameObject.SetActive( true );
		}
		
		SwitchStatesAsyncInfiniteLoop().Forget();
	}

	private				Service_GameFlow					_service;
	private				FlowNode							_root;
	private				FlowNode							_mainLineTip		= null!;
	private				FlowNode							_mainLineActive		= null!;
	private readonly	Dictionary<AssetRef<State>, State>	_stateInstances		= new( 32 );

	private		Int32			_uidNext = 1;
	private		Boolean			_doTransition;

	public		Service_GameFlow Service				=> _service;
	public		FlowNode		 Root					=> _root;

	public		FlowNode		MainLineActive			=> _mainLineActive;
	public		FlowNode		MainLineTip				=> _mainLineTip;

	public		StateHandle		Open					( AssetRef<GameStage> stageRef, GameContext parentContext = null, Object openParams = null, Scene spawnIn = default )
	{
		return Open( new AssetRef<State>(stageRef.Uid, stageRef.SubId), null, openParams, null, true, spawnIn, parentContext );	
	}
	public		StateHandle		Open					( AssetRef<State> stateRef, State callSource, Object openParams = null, FlowNode? parent = null, Boolean isLocked = false, Scene spawnIn = default, GameContext parentContext = null )
	{
		var newNode			= SpawnStateAndNode( stateRef, openParams, callSource, parent, isLocked, spawnIn );
		
		if (newNode.State is GameStage gs)
			gs.Setup( _service, parentContext );
		
		return newNode.Handle;
	}

	internal	StateHandle		GoBack					( )
	{
		if (_mainLineTip != _root && _mainLineTip.State.TryGoBack())
			RemoveNodesUpTo( _mainLineTip, _mainLineTip.Back );

		return _mainLineTip.Handle;
	}
	internal	void			RemoveNode				( FlowNode node )
	{
		RemoveNodesUpTo( node, node.Back );
	}
	internal	void			RemoveNodesUpTo			( FlowNode source, FlowNode target, Object openParams = null, Boolean skipCurrent = false )
	{
		if (source is not { IsValid: true } || source == _root)
			return;
		
		var iter	= source;

		if (skipCurrent)
			iter = iter.Back;

		while (iter != target)
		{
			if (iter == _root) 
				break;

			var toRemove	= iter;
			iter			= iter.Back!;
			
			if (toRemove.FirstChild != null)
				RemoveNodesUpTo( toRemove.FirstChild.GetLastSibling(), toRemove );
			
			if (toRemove == _mainLineTip)
				_mainLineTip  = iter;
			
			if (toRemove.Back != null)		toRemove.Back.Forward = toRemove.Forward;
			if (toRemove.Forward != null)	toRemove.Forward.Back = toRemove.Back;
			
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
	
	public		void			TransitionNow			( )
	{
		if (!_doTransition) 
			return;
		
		_doTransition = false;
		DoStateTransitions();
	}
	public		FlowNode		SpawnStateAndNode		( AssetRef<State> stateRef, Object openParams, State callSource, FlowNode? parent, Boolean isLocked,  Scene spawnIn )
	{
		var state = default(State);
		var instances = callSource?.GameStage?._stateInstances;
		instances?.TryGetValue( stateRef, out state );

		if (!state)
			_stateInstances.TryGetValue( stateRef, out state );
		
		var stateInstanceOrPrefab = state;
		
		if (!stateInstanceOrPrefab)
			stateInstanceOrPrefab = stateRef.LoadAssetSync();
		
		if (stateInstanceOrPrefab is GameStage gs)
		{
			instances = _stateInstances;
			parent = _root;
			isLocked = true;
			
			if (!state)
			{
				var activeSelf = stateInstanceOrPrefab.gameObject.activeSelf;
				stateInstanceOrPrefab.gameObject.SetActive( false );
				
				state = (State)UnityEngine.Object.Instantiate( stateInstanceOrPrefab, spawnIn.IsValid() ? spawnIn : Service.gameObject.scene );
				state.transform.SetSiblingIndex(0);
				
				stateInstanceOrPrefab.gameObject.SetActive( activeSelf );
				stateInstanceOrPrefab.gameObject.ClearEditorDirty();
			}
		}
		else
		{
			parent ??= callSource ? callSource.GameStage._node : _root.FirstChild.GetLastSibling();
			
			if (parent.State is GameStage gs2 && gs2.MainStateRef == stateRef && gs2.MainState != null)
				return gs2.OpenMainState(openParams).Node;		
			
			if (!state)
			{
				var activeSelf	= stateInstanceOrPrefab.gameObject.activeSelf;
				stateInstanceOrPrefab.gameObject.SetActive( false );
				
				var stateContainer = parent.State.GetSubStatesContainer();
				state = UnityEngine.Object.Instantiate( stateInstanceOrPrefab, stateContainer );
				
				stateInstanceOrPrefab.gameObject.SetActive( activeSelf );
				stateInstanceOrPrefab.gameObject.ClearEditorDirty();
			}
			
			if (instances == null)
				instances = ((GameStage)parent.GameStageNode.State)._stateInstances;
		}

		instances[stateRef] = state;
		state.name = state.name.Replace( "(Clone)", "" );
		state._prefabRef = stateRef;
		state._graph = this;
		
		var nextNode = new FlowNode
		{
			Graph		= this,
			Uid			= _uidNext++,
			State		= state, 
			OpenParams	= openParams, 
			IsLocked	= isLocked,
		};
		
		nextNode.PrevSibling = parent.FirstChild.GetLastSibling();
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
	
	public			void		ScheduleSwitchStates			( )	
	{
		_doTransition	= true;
	}
	private async	UniTask		SwitchStatesAsyncInfiniteLoop	( )	
	{
		while (Application.isPlaying && _root != null && _root.State)
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
		LiteTransitions.DoStateTransitions( ref _mainLineActive, _mainLineTip );
	}
	
#if UNITY_EDITOR
	public void DrawRuntimeUI( )
	{
		if (!Application.isPlaying || _root == null || !_root.State)
			return;
		
		var currStage = default(GameStage);

		// Draw root
		GUILayout.Space( 10 );
		GUILayout.BeginHorizontal();
		{
			GUILayout.Label( $"{_root.Uid:D3}", GUILayout.Width(30) );
			GUILayout.Label( $"{(_root.IsShowed ? "■" : "□")}", GUILayout.Width(20) );
			GUILayout.Label( $"{_root.State.name.Replace( "State", "", StringComparison.OrdinalIgnoreCase ).Trim('_')}" );
		}
		GUILayout.EndHorizontal();
		GUILayout.Space( 5 );
		
		for (var node = _root.Forward; node != null; node = node.Forward)
		{
			if (node.State is GameStage)
				GUILayout.Space( 5 );
				
			GUILayout.BeginHorizontal();
			GUILayout.Space( node.State is GameStage ? 10 : 26 );
			GUILayout.Label( $"{node.Uid:D3}", GUILayout.Width(30) );
			GUILayout.Label( $"{(node.IsShowed ? "■" : "□")}", GUILayout.Width(20) );
			GUILayout.Label( $"{node.State.name.Replace( "State", "", StringComparison.OrdinalIgnoreCase ).Trim('_')} {(node.OpenParams != null ? "op:" + node.OpenParams : "")}" );
			GUILayout.EndHorizontal();
		}
	}
#endif
}