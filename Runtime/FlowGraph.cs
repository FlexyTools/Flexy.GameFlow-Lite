namespace Flexy.GameFlow;

public class FlowGraph
{
	public FlowGraph	( GameFlowService service, AssetRef<State> rootStateRef )
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
			state.PrefabRef = rootStateRef;
		
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

	private				GameFlowService						_service;
	private				FlowNode							_root;
	private				FlowNode							_mainLineTip		= null!;
	private				FlowNode							_mainLineActive		= null!;
	private readonly	Dictionary<AssetRef<State>, State>	_stateInstances		= new( 32 );

	private		Int32			_uidNext = 1;
	private		Boolean			_doTransition;

	public		GameFlowService	Service					=> _service;
	public		FlowNode		Root					=> _root;

	public		FlowNode		MainLineActive			=> _mainLineActive;
	public		FlowNode		MainLineTip				=> _mainLineTip;

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
		_stateInstances.TryGetValue( stateRef, out var state );
		
		var stateInstanceOrPrefab = state;
		
		if (!stateInstanceOrPrefab)
			stateInstanceOrPrefab = stateRef.LoadAssetSync();
		
		if (stateInstanceOrPrefab is GameStage gs)
		{
			parent = _root;
			isLocked = true;
			
			if (!state)
			{
				var activeSelf = stateInstanceOrPrefab.gameObject.activeSelf;
				stateInstanceOrPrefab.gameObject.SetActive( false );
				state = (State)UnityEngine.Object.Instantiate( stateInstanceOrPrefab, spawnIn.IsValid() ? spawnIn : Service.gameObject.scene );
				state.transform.SetSiblingIndex(0);
				stateInstanceOrPrefab.gameObject.SetActive( activeSelf );
			}
		}
		else
		{
			if (parent == null)
			{
				var stage = !callSource ? (GameStage)_root.FirstChild.GetLastSibling().State : callSource.GameStage;
				parent = stage.Node;
		
				if( state == stage.MainState )	
					return stage.OpenMainState(openParams).Node;		
			}
			
			if (!state)
			{
				var activeSelf	= stateInstanceOrPrefab.gameObject.activeSelf;
				stateInstanceOrPrefab.gameObject.SetActive( false );
				var stateContainer = parent.State.GetSubStatesContainer();
				state = UnityEngine.Object.Instantiate( stateInstanceOrPrefab, stateContainer );
				stateInstanceOrPrefab.gameObject.SetActive( activeSelf );
			}
		}

		_stateInstances[stateRef] = state;
		state.name = state.name.Replace( "(Clone)", "" );
		state.PrefabRef = stateRef;
		
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
		var prevNode		= _mainLineActive; 
		var nextNode		= _mainLineTip;
	
		var isMoveForward	= nextNode.Uid > prevNode.Uid;
		var commonParent	= FindNearestCommonParent( prevNode, nextNode );

		var closingBranchNode = prevNode;
		while (closingBranchNode != commonParent)
		{
			try{ closingBranchNode.State.gameObject.SetActive( false );	} catch (Exception ex) { Debug.LogException( ex ); }
			try{ NodeStateHide( closingBranchNode, isMoveForward );		} catch (Exception ex) { Debug.LogException( ex ); }

			closingBranchNode = closingBranchNode.Parent;
			
			if (!isMoveForward && closingBranchNode.FirstChild == null)
				closingBranchNode.State.DoLastChildHide();
		}
		
		var openingBranchNode = commonParent.FirstChild.GetLastSibling();
		
		if (openingBranchNode == null)
			_mainLineActive = commonParent;
		
		while (openingBranchNode != null)
		{
			_mainLineActive = openingBranchNode;
		
			if (isMoveForward && openingBranchNode.Parent.FirstChild.NextSibling == null)
				openingBranchNode.Parent.State.DoFirstChildShow();
		
			try{ NodeStateShow( openingBranchNode, isMoveForward );		} catch (Exception ex) { Debug.LogException( ex ); }
			try{ openingBranchNode.State.gameObject.SetActive( true );	} catch (Exception ex) { Debug.LogException( ex ); }
			
			openingBranchNode = openingBranchNode.FirstChild.GetLastSibling();
		}

		return;

		static FlowNode	FindNearestCommonParent	( FlowNode a, FlowNode b )
		{
			var aSet = new HashSet<FlowNode>();

			for ( ; a != null; a = a.Parent)
				aSet.Add( a );

			for ( ; b != null; b = b.Parent)
				if (aSet.Contains( b ))
					return b;

			throw new InvalidOperationException("Graph broken, can not find common parent, it must be at least one common parent -> Root of the graph ");
		}
		
		static void	NodeStateShow	( FlowNode nextNode, Boolean isMoveForward )
		{
			var nextWasShown		= nextNode.WasShown;
			nextNode.State._node	= nextNode;
			nextNode.WasShown		= true;
			
			var state = nextNode.State;
		
			if( !nextWasShown && !isMoveForward )
				try						{ state.DoShow( ); }
				catch ( Exception ex )	{ Debug.LogException( ex );		}

			try
			{
				if( isMoveForward ) state.DoShow( );
				else				state.DoBackShow( );
			}
			catch ( Exception ex ) { Debug.LogException( ex ); }
		}
		static void	NodeStateHide	( FlowNode prevNode, Boolean isMoveForward )
		{
			var state = prevNode.State;
		
			try
			{
				if( isMoveForward )	state.DoFwdHide( );
				else				state.DoHide( );
			}
			catch ( Exception ex ) { Debug.LogException( ex ); }
		}
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