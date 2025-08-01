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
		
			state.name = state.name.Replace( "(Clone)", "" );
			state.SetSelfRef( rootStateRef );
		
			_root = new FlowNode
			{
				Graph		= this,
				Uid			= _uidNext++,
				State		= state, 
				OpenParams	= null, 
				IsPreserved	= true,
				WasShowed	= true
			};

			_activeNode = _root;
			_currentNode = _root;
			
			state.gameObject.SetActive( true );
		}
		
		SwitchStatesAsyncInfiniteLoop().Forget();
	}

	private				GameFlowService						_service;
	private				FlowNode							_root				= new();
	private				FlowNode							_activeNode			= null!;
	private				FlowNode							_currentNode		= null!;
	private readonly	Dictionary<AssetRef<State>, State>	_stateInstances		= new( 32 );

	private		Int32			_uidNext = 1;
	private		Boolean			_doTransition;

	public		GameFlowService	Service					=> _service;
	public		FlowNode		Root					=> _root;

	public		FlowNode		ActiveStateNode			=> _activeNode;
	public		FlowNode		CurrentStateNode		=> _currentNode;

	public		StateHandle		Open					( AssetRef<State> stateRef, State callSource, Object openParams = null, Scene spawnIn = default, GameContext parentContext = null, FlowNode? parent = null )
	{
		var newNode			= SpawnStateAndNode( stateRef, openParams, callSource, spawnIn, parent );
		
		if (parentContext != null && newNode.State is GameStage gs)
			gs.SetContext( parentContext );
		
		return newNode.GetHandle();
	}

	internal	FlowNode		AddNode					( State state, Object openParams, Boolean isPreserved = false )
	{
		var newNode = new FlowNode
		{
			Graph = this,
			Uid = _uidNext++,
			State = state, 
			OpenParams = openParams, 
			IsPreserved = isPreserved,
		};

		_activeNode ??= newNode;
		
		_currentNode.Forward = newNode;
		newNode.Back = _currentNode;
		_currentNode = newNode;

		_currentNode.NextSibling = newNode;

		ScheduleSwitchStates( );

		return newNode;
	}
	internal	void			RemoveNode				( StateHandle handle )
	{
		if ( !handle.IsValid || handle.Node == _root )
			return;

		if( handle.Node == _currentNode )
			RemoveNodesUpTo( _currentNode.Back );
	}
	internal	void			RemoveNodesUpTo			( FlowNode target, Object openParams = null, Boolean skipCurrent = false )
	{
		var first	= _root;
		var iter	= _currentNode;

		if (skipCurrent)
			iter = iter.Back;

		while (iter != target)
		{
			if (iter == null) return;
			if (iter == first) break;

			var toRemove= iter;
			iter		= iter.Back!;
			
			var b = toRemove.Back;
			var f = toRemove.Forward;
			
			b.Forward = f;
			if (f is not null)
				f.Back = b;
		}

		if (iter == null) 
			return;

		if (openParams != null)
			iter.OpenParams = openParams;

		_currentNode = iter;

		ScheduleSwitchStates( );
	}
	internal	StateHandle		GoBack					( )
	{
		if (_currentNode != _root && _currentNode.State.TryGoBack())
			RemoveNodesUpTo( _currentNode.Back );

		return _currentNode.GetHandle( );
	}
	
	public		void			TransitionNow			( )
	{
		if (!_doTransition) 
			return;
		
		_doTransition = false;
		TransitionState();
	}
	public		FlowNode		SpawnStateAndNode		( AssetRef<State> stateRef, Object openParams, State callSource, Scene spawnIn, FlowNode? parent )
	{
		_stateInstances.TryGetValue( stateRef, out var state );

		var statePrefab	= stateRef.LoadAssetSync();
		var activeSelf	= statePrefab.gameObject.activeSelf;
		
		FlowNode newFlowNode;
		
		if (statePrefab is GameStage gs)
		{
			parent = _root;
			
			if (!state)
			{
				statePrefab.gameObject.SetActive( false );
				state = (State)UnityEngine.Object.Instantiate( statePrefab, spawnIn.IsValid() ? spawnIn : Service.gameObject.scene );
				state.transform.SetSiblingIndex(0);
				statePrefab.gameObject.SetActive( activeSelf );
			}
		}
		else
		{
			if (parent == null)
			{
				var stage = !callSource ? (GameStage)_root.FirstChild.GetLastSibling().State : callSource.GameStage;
				parent = stage._node;
		
				if( state == stage.MainState )	
					return stage.OpenMainState(openParams).Node;		
			}
			
			if (!state)
			{
				statePrefab.gameObject.SetActive( false );
				var stateContainer = parent.State.GetSubStatesContainer();
				state = UnityEngine.Object.Instantiate( statePrefab, stateContainer );
				statePrefab.gameObject.SetActive( activeSelf );
			}
		}

		_stateInstances[stateRef] = state;
		state.name = state.name.Replace( "(Clone)", "" );
		state.SetSelfRef( stateRef );
		
		newFlowNode = AddNode( state, openParams );
		newFlowNode.Parent = parent;
		
		if (parent.FirstChild == null)
			parent.FirstChild = newFlowNode;
		
		newFlowNode.Parent = _root;
		newFlowNode.PrevSibling = _root.Forward.GetLastSibling();
		newFlowNode.PrevSibling.NextSibling = newFlowNode;
			
		_currentNode.Forward = newFlowNode;
		newFlowNode.Back = _currentNode;
		
		_currentNode = newFlowNode;
		
		return newFlowNode;
	}
	public		void			ScheduleSwitchStates	( )
	{
		_doTransition	= true;
	}

	private async	UniTask		SwitchStatesAsyncInfiniteLoop	( )	
	{
		while (Application.isPlaying && _root == null)
			await UniTask.NextFrame();
	
		while (Application.isPlaying && _root != null && _root.State)
		{
			// Change view at last update (before animations)
			// This will allow to make many changes in update and then only one view transition
			await UniTask.NextFrame( PlayerLoopTiming.LastUpdate );

			if (!_doTransition)
				continue;

			_doTransition	= false;

			try						{ TransitionState(); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
		}
	}
	private 		void		TransitionState					( )	
	{
		var prevNode		= _activeNode; 
		var nextNode		= _currentNode;
	
		var isMoveForward	= nextNode.Uid > prevNode.Uid;

		var prevState		= prevNode.State;
		var nextState		= nextNode.State;

		var nextWasShown	= nextNode.WasShowed;

		if( nextState )
		{
			_activeNode		= nextNode;
			nextNode.WasShowed = true;
			nextNode.State._node = nextNode;
		}
		
		if (nextNode == prevNode && nextNode == _root) // This is possible only on very first substate open in state or first gameStage in history
		{
			isMoveForward = true;
			try{ StateShow( nextState, isMoveForward, nextNode.Uid, nextWasShown );	} catch (Exception ex) { Debug.LogException( ex ); }
			try{ nextState.gameObject.SetActive( true );} catch (Exception ex) { Debug.LogException( ex ); }
		}
		else
		{
			if (prevState is GameStage == nextState is GameStage)
			{
				try{ prevState.gameObject.SetActive( false );	} catch (Exception ex) { Debug.LogException( ex ); }
				try{ StateHide ( prevState, isMoveForward );	} catch (Exception ex) { Debug.LogException( ex ); }
			
				try{ StateShow( nextState, isMoveForward, nextNode.Uid, nextWasShown );	} catch (Exception ex) { Debug.LogException( ex ); }
				try{ nextState.gameObject.SetActive( true );} catch (Exception ex) { Debug.LogException( ex ); }
			}
			else
			{
				var pgs = prevState as GameStage; 
				var ngs = nextState as GameStage;
				
				if (pgs != null && nextState.GameStage == pgs)
				{
					try{ StateShow( nextState, isMoveForward, nextNode.Uid, nextWasShown );	} catch (Exception ex) { Debug.LogException( ex ); }
					try{ nextState.gameObject.SetActive( true );} catch (Exception ex) { Debug.LogException( ex ); }
				}
				else if (ngs != null && prevState.GameStage == ngs)
				{
					try{ prevState.gameObject.SetActive( false );	} catch (Exception ex) { Debug.LogException( ex ); }
					try{ StateHide ( prevState, isMoveForward );	} catch (Exception ex) { Debug.LogException( ex ); }	
				}
			}
		} 
		
		static void	StateShow		( State state, Boolean isMoveForward, Int32 Uid, Boolean nextWasShown )
		{
			state.SetUid( Uid );
 
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
		static void	StateHide		( State state, Boolean isMoveForward )
		{
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
		if ( !Application.isPlaying || _root == null || !_root.State )
			return;
		
		var currStage = default(GameStage);

		for (var node = _root; node != null; node = node.Forward)
		{
			var gameStage = node.State.GameStage;
		
			if(gameStage != currStage)
			{
				currStage = gameStage;
				GUILayout.EndVertical( );
				GUILayout.EndHorizontal( );
				GUILayout.Space( 5 );
		
				GUILayout.Label( gameStage.name );
				GUILayout.Space( 2 );

				GUILayout.BeginHorizontal( );
				GUILayout.Space( 16 );
				GUILayout.BeginVertical( );
			}
			
			GUILayout.BeginHorizontal();
			GUILayout.Label( $"{node.Uid:D3}", GUILayout.Width(30) );
			GUILayout.Label( $"{(node.IsActive ? "■" : "□")}", GUILayout.Width(20) );
			GUILayout.Label( $"{node.State.name.Replace( "State", "", StringComparison.OrdinalIgnoreCase ).Trim('_')} {(node.OpenParams != null ? "op:" + node.OpenParams : "")}" );
			GUILayout.EndHorizontal();
		}
	}
#endif
}

public class FlowNode
{
	public	FlowGraph	Graph;

	public	Int32		Uid = 1;
	public	Boolean		WasShowed; // used to call OnShow in case first show will be BackShow
	public	Boolean		IsPreserved;
	public	Object		OpenParams;
	public	State		State;

	public	FlowNode?	PrevSibling;
	public	FlowNode?	NextSibling;
	public	FlowNode?	Back;
	public	FlowNode?	Forward;
	public	FlowNode?	Parent;
	public	FlowNode?	FirstChild;
	
	public	Boolean		IsActive	=> Graph.ActiveStateNode == this;
	public	Boolean		IsCurrent	=> Graph.CurrentStateNode == this;
	public	FlowNode?	LeftNode	=> PrevSibling ?? Back ?? Parent;
	
	public override String			ToString	( )
	{
		return $"{Uid:D3} {(IsActive ? "■ " : "□ ")} {State.name.Replace( "State", "", StringComparison.OrdinalIgnoreCase ).Trim('_')} {(OpenParams != null ? "op:" + OpenParams : "")}";
	}

	public StateHandle GetHandle() => new(Uid){Node = this};

	public StateHandle Close()
	{
		var h = GetHandle();
		Graph.RemoveNode(GetHandle());
		return h;
	}

	public FlowNode GetLastSibling()
	{
		var node = this;
		for (;node.NextSibling != null; node = node.NextSibling);
		return node;
	}
}