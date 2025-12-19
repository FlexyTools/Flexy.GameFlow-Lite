using System.Linq;

namespace Flexy.GameFlow
{
	[HelpURL("https://github.com/FlexyTools/Flexy.Docs/blob/main/Framework/Flexy.GameFlow/ScriptingApi/Service_GameFlow.md")]

	[RequireComponent(typeof(GameContext))]
	public class Service_GameFlow : GameStage, IService
	{
		[SerializeField]	FlowLibrary		_rootFlowLibrary = null!;
		
		#if UNITY_INPUT_SYSTEM
		[SerializeField]	UnityEngine.InputSystem.InputActionReference?	_backInputActionRef;
		#endif

		private readonly	Dictionary<String, AssetRef<State>>	_statesDict = new (256);
		private readonly	Dictionary<AssetRef<State>, String>	_statesDictReverse = new (256);

		public		void			OrderedInit		( GameContext ctx )										
		{
			Debug.Log( $"[Service_GameFlow] Init" );
			name = "[GameFlow] (GlobalContext)";
			ReadLibrary();
			_graph = new(this);
		}
		public		FlowNode		Open<T>			( State src, Object? openParams = null ) where T: State	
		{
			var opener = GetOpener_ByStateType<T>(src);
			return opener.Ctx.Open(openParams);
		}
								
		public		Opener			GetOpener_ById				( State src, String croppedOrFullGuid )	
		{
			return new() { Ctx = new( _statesDict.GetValueOrDefault(croppedOrFullGuid), src ) };
		}
		public		Opener			GetOpener_ByStateType<T>	( State src ) where T : State			
		{
			return new() { Ctx = new( FindOpener( typeof(T) ), src ) };
		}
		public		T				GetOpener_ByOpenerType<T>	( State src ) where T : struct, IOpener	
		{
			return new() { Ctx = new( FindOpener( typeof(T).DeclaringType ), src ) };
		}
		public		String			GetRefTypeName				( AssetRef<State> stateRef )			
		{
			_statesDictReverse.TryGetValue(stateRef, out var name);
			return name;
		}

		protected virtual	void	Update			( )					
		{
#if UNITY_INPUT_SYSTEM
			if (_backInputActionRef?.ToInputAction().WasPressedThisFrame() ?? false)
				Graph.TryGoBack();
#endif
		}

		private		void			ReadLibrary		( )					
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
		private		AssetRef<State>	FindOpener		( Type typeToFind )	
		{
			if (_statesDict.TryGetValue(typeToFind.FullName, out var refState) || _statesDict.TryGetValue(typeToFind.Name, out refState))
				return refState;
				
			return default;
		}
	}
	
#if UNITY_EDITOR
	[UnityEditor.CustomEditor(typeof(Service_GameFlow), editorForChildClasses:true)]
	public class Service_GameFlowEditor : Editor_WithRuntimeGui
	{
		public override UnityEngine.UIElements.VisualElement CreateInspectorGUI( )		
		{
			_root = new UnityEngine.UIElements.VisualElement{ name = "Editor_WithRuntimeGui" } ;
			
			AddPropertiesExcluding("_mainStateRef", "_statesContainer");
			AddIMGUIInspectorAndRuntimeOne();
	        
			return _root;
		}
	}
#endif
}