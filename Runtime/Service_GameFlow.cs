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

		private readonly	Dictionary<String, AssetRef<State>>	_refsDict	= new (512);
		private readonly	Dictionary<AssetRef<State>, Type>	_typesDict	= new (128);
		
		public		FlowNode		Open<T>			( FlowNode src, Object? openParams = null ) where T: State	
		{
			var opener = GetOpener_ByStateType<T>(src);
			return opener.Ctx.Open(openParams);
		}
		
		public		Opener			GetOpener_ByStateType<T>	( FlowNode src, String? guid = null ) where T : State			
		{
			var stateRef = guid != null ? _refsDict.GetValueOrDefault(guid) : FindOpener( typeof(T) );
			return new() { Ctx = new( stateRef, src ) };
		}
		public		T				GetOpener_ByOpenerType<T>	( FlowNode src, String? guid = null ) where T : struct, IOpener	
		{
			var stateRef = guid != null ? _refsDict.GetValueOrDefault(guid) : FindOpener( typeof(T).DeclaringType! );
			return new() { Ctx = new( stateRef, src ) };
		}
		public		Type			GetRefType					( AssetRef<State> stateRef )					
		{
			_typesDict.TryGetValue(stateRef, out var type);
			return type;
		}

		protected override	void	Awake			( )					
		{
			Debug.Log( $"[Service_GameFlow] Init" );
			name = "[GameFlow] (GlobalContext)";
			ReadLibrary();
			_graph = new(this);
			
			base.Awake();
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

			foreach (var pair in allRegisteredStates)
			{
				var type = Type.GetType(pair.TypeFullName);
				if (pair.Ref.IsNone || String.IsNullOrWhiteSpace( pair.TypeFullName ) || type is null )
				{
					Debug.LogError( $"[GameFlowService] ReadLibrary: invalid entry: ref:{pair.Ref} name:{pair.TypeFullName}" );
					continue;
				}
				
				_typesDict.Add(pair.Ref, type);	
				
				var refStr = pair.Ref.ToString()[..32];
				var refStr2 = refStr[..7];

				Debug.Log( $"[GameFlowService] ReadLibrary   {refStr.Replace("[", "  [").Insert(7, "  ")} => {type.FullName.Insert(type.FullName.LastIndexOf('.')+1, "  ")}" );
				
				_refsDict.TryAdd( type.FullName, pair.Ref );
				_refsDict.TryAdd( type.Name, pair.Ref );
				_refsDict.TryAdd( refStr, pair.Ref );
				_refsDict.TryAdd( refStr2, pair.Ref );
			}

			Debug.Log( $"[GameFlowService] ReadLibrary: done" );
		}
		private		AssetRef<State>	FindOpener		( Type typeToFind )	
		{
			if (_refsDict.TryGetValue(typeToFind.FullName, out var refState) || _refsDict.TryGetValue(typeToFind.Name, out refState))
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