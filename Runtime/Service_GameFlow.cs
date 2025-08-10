using System.Linq;

namespace Flexy.GameFlow
{
	public class Service_GameFlow : BindableBehaviour, IService
	{
		[FormerlySerializedAs("_lib")] 
		[SerializeField]	FlowLibrary		_rootFlowLibrary;
		[SerializeField]	AssetRef<State> _rootStateRef;
		#if UNITY_INPUT_SYSTEM
		[SerializeField]	UnityEngine.InputSystem.InputActionReference	_backInputActionRef;
		#endif

		private readonly	Dictionary<String, AssetRef<State>>	_statesDict = new ( 256 );
		private readonly	Dictionary<AssetRef<State>, String>	_statesDictReverse = new ( 256 );

		public				FlowGraph		Graph				{ get; private set; }

		public				void			OrderedInit			( GameContext ctx )	
		{
			Debug.Log( $"[GameFlowService] Init" );
			ReadLibrary();
			Graph = new(this, _rootStateRef);
		}
		protected virtual	void			Update				( )					
		{
			#if UNITY_INPUT_SYSTEM
			if (_backInputActionRef?.ToInputAction().WasPressedThisFrame())
				Graph.GoBack();
			#endif
		}
		
		public			State.Opener		GetOpener_FromId			( String croppedOrFullId, State src )	
		{
			return new() { Ctx = new( _statesDict.GetValueOrDefault(croppedOrFullId), src ) };
		}
		public			State.Opener		GetOpener_FromStateType<T>	( State src ) where T : State			
		{
			return new() { Ctx = new( FindOpener( typeof(T) ), src ) };
		}
		public			T					GetOpener_FromOpenerType<T>	( State src ) where T : struct, IOpener	
		{
			return new() { Ctx = new( FindOpener( typeof(T).DeclaringType ), src ) };
		}
		public			String				GetRefTypeName				( AssetRef<State> stateRef )			
		{
			_statesDictReverse.TryGetValue(stateRef, out var name);
			return name;
		}

		private			void				ReadLibrary			( )					
		{
			Debug.Log( $"[GameFlowService] ReadLibrary: start..." );

			var allRegisteredStates = new List<FlowLibrary.StateRef>( _rootFlowLibrary.CollectStates().Distinct().OrderBy(i => i.TypeFullName) );

			foreach (var statePair in allRegisteredStates)
			{
				if (statePair.Ref.IsNone || String.IsNullOrWhiteSpace( statePair.TypeFullName ))
				{
					Debug.LogError( $"[GameFlowService] ReadLibrary: invalid entry: ref:{statePair.Ref} name:{statePair.TypeFullName}" );
					continue;
				}
				
				var fullName = statePair.TypeFullName;
				var shortName = fullName[(fullName.LastIndexOf('.')+1)..];
				var refStr = statePair.Ref.ToString()[..32];
				var refStr2 = refStr[..7];

				Debug.Log( $"[GameFlowService] ReadLibrary   {refStr.Replace("[", "  [").Insert(7, "  ")} => {fullName.Insert(fullName.LastIndexOf('.')+1, "  ")}" );
				
				_statesDict.TryAdd( fullName, statePair.Ref );
				_statesDict.TryAdd( shortName, statePair.Ref );
				_statesDict.TryAdd( refStr, statePair.Ref );
				_statesDict.TryAdd( refStr2, statePair.Ref );
			}

			foreach (var pair in allRegisteredStates)
				_statesDictReverse.Add(pair.Ref, pair.TypeFullName);	

			Debug.Log( $"[GameFlowService] ReadLibrary: done" );
		}
		private			AssetRef<State>		FindOpener			( Type typeToFind )	
		{
			if (_statesDict.TryGetValue(typeToFind.FullName, out var refState) || _statesDict.TryGetValue(typeToFind.Name, out refState))
				return refState;
				
			return default;
		}
		
		#if UNITY_EDITOR
		[RuntimeInspectorGui( Repaint = true )]
		private void DrawRuntimeUI( )
		{
			if (!Application.isPlaying)
				return;

			GUILayout.Space( 16 );
			Graph.DrawRuntimeUI();
		}
		#endif
	}
}