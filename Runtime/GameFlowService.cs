using System.Linq;

namespace Flexy.GameFlow
{
	public class GameFlowService : APropertyBindableBehaviour, IService
	{
		[FormerlySerializedAs("_lib")] 
		[SerializeField]	FlowLibrary		_rootFlowLibrary;
		[SerializeField]	AssetRef<State> _rootStateRef;
		[SerializeField]	EBackInputHandling	_backInputHandling = EBackInputHandling.OldInputManager;
		#if UNITY_INPUT_SYSTEM
		[SerializeField]	UnityEngine.InputSystem.InputActionReference	_backInputAction;
		#endif

		private				FlowGraph		_flowGraph;

		private	readonly	List<FlowLibrary.StateRef>			_flattenedLibrary = new ( 64 );
		private readonly	Dictionary<String, AssetRef<State>>	_stateRefByType = new ( 64 );

		public				FlowGraph		FlowGraph			=> _flowGraph;
		
		public				void			OrderedInit			( GameContext ctx )
		{
			Debug.Log( $"[GameFlowService] Init" );
			ReadLibrary( );
			_flowGraph = new(this, _rootStateRef);
		}
		protected virtual	void			Update				( )
		{
	        // Check if Back (esc or equivalent on other platforms) was pressed this frame
	        if (_backInputHandling == EBackInputHandling.OldInputManager)
		    {
		        if (Input.GetKeyDown( KeyCode.Escape ))
			        FlowGraph.GoBack();
			}
	        else if (_backInputHandling == EBackInputHandling.NewInputSystem)
			{
				#if UNITY_INPUT_SYSTEM
				if (_backInputAction.ToInputAction().WasPressedThisFrame())
					MainStateHistory.GoBack();
				#else
				Debug.LogError( "Input System Package is not enabled in project" );
				#endif
				
			}
		}
		
		public			StateHandle			OpenGameStage		( GameStage parent, AssetRef<GameStage> gameStageRef, Object openParams = null )
		{
			return OpenGameStage( gameStageRef, openParams, parent.gameObject.scene, parent.Context );
		}
		public			StateHandle			OpenGameStage		( AssetRef<GameStage> gameStageRef, Object openParams = null, Scene spawnIn = default, GameContext parentContext = null )
		{
			if( spawnIn == default )
			 	spawnIn = gameObject.scene;
		
			var handle = _flowGraph.Open( new AssetRef<State>(gameStageRef.Uid, gameStageRef.SubId), null, (openParams, parentContext), spawnIn );
			
			return handle;
		}
		
		public			State.Opener		GetOpener_FromId			( String cropId, State src )
		{
			for ( var i = 0; i < _flattenedLibrary.Count; i++ )
			{
				var stateRef = _flattenedLibrary[i];

				if ( stateRef.Ref.Uid.ToString().StartsWith( cropId ) )
					return new() { Ctx = new( stateRef.Ref, src ) };
			}

			return default;
		}
		public			State.Opener		GetOpener_FromStateType<T>	( State src ) where T : State
		{
			return new() { Ctx = new( FindOpener( typeof(T) ), src ) };
		}
		public			T					GetOpener_FromOpenerType<T>	( State src ) where T : struct, IOpener
		{
			var declaringType		= typeof(T).DeclaringType;
			var wndType				= declaringType;

			return new() { Ctx = new( FindOpener( wndType ), src ) };
		}

		private			void				ReadLibrary		( )
		{
			Debug.Log( $"[GameFlowService] BuildRegistry: start..." );

			_flattenedLibrary.Clear();
			_flattenedLibrary.AddRange( _rootFlowLibrary.CollectStates().Distinct() );

			var states			= _flattenedLibrary;
			var assemblies		= AppDomain.CurrentDomain.GetAssemblies().ToList();
			var lastAssembly	= assemblies[0];

			for ( var i = 0; i < states.Count; i++ )
			{
				var state	= states[i];

				if( state.Ref.IsNone || String.IsNullOrWhiteSpace( state.TypeFullName ) )
					continue;

				if( _stateRefByType.ContainsKey( state.TypeFullName ) )
					continue;

				if( String.IsNullOrWhiteSpace( state.TypeFullName ) )
				{
					Debug.LogError( $"[GameFlowService] BuildRegistry: {state.Ref} has no type full name! Skipping" );
					continue;
				}

				if( lastAssembly.GetType( state.TypeFullName, false, true ) is { } type )
				{
					RegisterState( i, state, type );
					continue;
				}

				foreach ( var a in assemblies )
				{
					lastAssembly = a;

					if( a.GetType( state.TypeFullName, false, true ) is {} t )
					{
						RegisterState( i, state, t );
						break;
					}
				}

				continue;

				void RegisterState( Int32 index, FlowLibrary.StateRef wnd, Type wndType )
				{
					Debug.Log( $"[GameFlowService] BuildRegistry: {wnd.TypeFullName} \t=> {wndType.Name}" );
					_stateRefByType.Add( wnd.TypeFullName, wnd.Ref );
					var rw = _flattenedLibrary[index];
					rw.Type = wndType;
					_flattenedLibrary[index] = rw;
				}
			}

			Debug.Log( $"[GameFlowService] BuildRegistry: done" );
		}
		private			AssetRef<State>		FindOpener			( Type typeToFind )
		{
			if( _stateRefByType.TryGetValue( typeToFind.FullName, out var state ) )
				return state;

			do
			{
				foreach (var stateRef in _flattenedLibrary)
				{
					if ( !typeToFind.IsAssignableFrom( stateRef.Type ) )
						continue;

					_stateRefByType.Add( typeToFind.FullName, stateRef.Ref );

					return stateRef.Ref;
				}

				typeToFind = typeToFind.BaseType;
			}
			while ( typeToFind != typeof(State) );

			return default;
		}

		private enum EBackInputHandling
		{
			Disabled,
			OldInputManager,
			NewInputSystem
		}
		
		#if UNITY_EDITOR
		[RuntimeInspectorUI( Repaint = true)]
		private void DrawRuntimeUI( )
		{
			if (!Application.isPlaying)
				return;

			GUILayout.Space( 16 );
			_flowGraph.DrawRuntimeUI();
		}
		#endif
	}
}