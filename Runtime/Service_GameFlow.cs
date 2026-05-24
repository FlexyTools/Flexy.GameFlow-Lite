using System.Linq;

namespace Flexy.GameFlow
{
	[HelpURL("https://github.com/FlexyTools/Flexy.Docs/blob/main/Framework/Flexy.GameFlow/ScriptingApi/Service_GameFlow.md")]

	[RequireComponent(typeof(GameContext))]
	public class Service_GameFlow : GameStage, IService
	{
		[SerializeField]	FlowLibrary		_rootFlowLibrary = null!;
		
		#if ENABLE_INPUT_SYSTEM
		[SerializeField]	UnityEngine.InputSystem.InputActionReference?	_backInputActionRef;
		#endif

		private readonly	Dictionary<String, AssetRef<State>>	_refsDict	= new (512);
		private readonly	Dictionary<AssetRef<State>, Type>	_typesDict	= new (128);
		
		public		FlowNode		Open<T>			( FlowNode src, object? openParams = null ) where T: State	
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
			Debug.Log( "Init" );
			name = "[GameFlow] (GlobalContext)";
			RegisterLibrary(_rootFlowLibrary);
			_graph = new(this);
			
			base.Awake();
		}
		protected virtual	void	Update			( )					
		{
#if ENABLE_INPUT_SYSTEM
			if (_backInputActionRef?.ToInputAction().WasPressedThisFrame() ?? false)
				Graph.TryGoBack();
#endif
		}

		public		void			RegisterLibrary	( FlowLibrary library )	
		{
			Debug.Log( $"Start..." );

			var stateRefs = new List<FlowLibrary.StateRef>( library.CollectStates().Distinct().OrderBy(i => i.TypeFullName) );

			foreach (var state in stateRefs)
			{
				var type = Type.GetType(state.TypeFullName);
				if (state.Ref.IsNone || String.IsNullOrWhiteSpace( state.TypeFullName ) || type is null )
				{
					Debug.LogError( $"Invalid Entry: ref:{state.Ref} name:{state.TypeFullName}" );
					continue;
				}
				
				_typesDict.Add(state.Ref, type);	
				
				var refStr = state.Ref.ToString()[..32];
				var refStr2 = refStr[..7];

				Debug.Log( $"   {refStr.Replace("[", "  [").Insert(7, "  ")} => {type.FullName.Insert(type.FullName.LastIndexOf('.')+1, "  ")}" );
				
				_refsDict.TryAdd( type.FullName, state.Ref );
				_refsDict.TryAdd( type.Name, state.Ref );
				_refsDict.TryAdd( refStr, state.Ref );
				_refsDict.TryAdd( refStr2, state.Ref );
			}

			Debug.Log( $"Done" );
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