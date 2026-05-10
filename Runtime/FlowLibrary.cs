using System.Linq;
using Flexy.AssetRefs.Pipelines;

namespace Flexy.GameFlow
{
	[HelpURL("https://github.com/FlexyTools/Flexy.Docs/blob/main/Framework/Flexy.GameFlow/ScriptingApi/FlowLibrary.md")]

    [CreateAssetMenu(fileName = "GameFlow.lib.asset", menuName = "Flexy/GameFlows/Library")]
	public class FlowLibrary : ScriptableObject, IAssetRefsSource
	{
		[SerializeField]	EStateGrabMode	_autoGrabMode	= EStateGrabMode.GrabFromCurrentDir;
		[SerializeField]	FlowLibrary[]?	_dependencies	= null;
		[SerializeField]	StateRef[]		_states			= null!;

		public	List<AssetRef>	CollectAssets	( )										
		{
			#if UNITY_EDITOR
			GrabStates();
			#endif
		
			var list = _states.Select( w => w.Ref.Raw ).ToList();
			
			if (_dependencies != null)
				foreach (var library in _dependencies)
					list.AddRange( library.CollectAssets() );

			return list;
		}
		public	List<StateRef>	CollectStates	( Boolean includeDependencies = true )	
		{
			var list = new List<StateRef>();

			#if UNITY_EDITOR
			GrabStates();
			#endif

			list.AddRange(_states);

			if (includeDependencies && _dependencies != null)
				foreach (var dep in _dependencies)
					if (dep != null)
						list.AddRange( dep.CollectStates() );

			return list;
		}

#if UNITY_EDITOR
		private	void			OnValidate		( )		
		{
			GrabStates(false);
		}
		
		private	void			GrabStates		( Boolean saveAssets = true )
		{
			if (_autoGrabMode is not EStateGrabMode.NoAutoGrab)
			{
				EditorGrabStates(_autoGrabMode);
			}
			else
			{
				for (var i = 0; i < _states.Length; i++)
				{
					var type = AssetLoader.EditorLoadAsset( _states[i].Ref )?.GetType();
					var newName = type == null ? "" : $"{type.FullName}, {type.Assembly.GetName().Name}";
					if (_states[i].TypeFullName != newName)
					{
						_states[i].TypeFullName = newName; 
						UnityEditor.EditorUtility.SetDirty(this);
					}
				}
			}
			
			if (saveAssets)
				UnityEditor.AssetDatabase.SaveAssets();
		}
		
		[ContextMenu("Revalidate")]
		internal	void	RevalidateStates					( )			
		{
			GrabStates();
		}
		[ContextMenu("Grab States Curr Dir")]
		internal	void	EditorGrabStatesCurrDir				( )			
		{
			EditorGrabStates(EStateGrabMode.GrabFromCurrentDir);
		}
		[ContextMenu("Grab States Curr Dir and Sub Dirs")]
		internal	void	EditorGrabStatesCurrDirAndSubDirs	( )			
		{
			EditorGrabStates(EStateGrabMode.GrabFromCurrentDirAndSubDirs);
		}
		
		private		void	EditorGrabStates	( EStateGrabMode grabMode )	
		{
			if (grabMode is EStateGrabMode.NoAutoGrab)
				return;
		
			var path	= UnityEditor.AssetDatabase.GetAssetPath(this);
			
			if (path == "")
				return;
				
			var curDir	= System.IO.Path.GetDirectoryName(path);
			var guids	= UnityEditor.AssetDatabase.FindAssets("t:prefab", new[] { curDir });
			var states	= new List<StateRef>();
			
			foreach (var guid in guids)
			{
				var assetPath	= UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
				
				if (grabMode is EStateGrabMode.GrabFromCurrentDir && System.IO.Path.GetDirectoryName(assetPath) != curDir)
					continue;
				
				var prefab		= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
				var state		= prefab.GetComponent<State>();
				
				if (state != null)
				{
					states.Add(new StateRef { 
						Ref = new AssetRef<State>(Hash128.Parse(guid)),
						TypeFullName = $"{state.GetType().FullName}, {state.GetType().Assembly.GetName().Name}"
					});
				}
			}

			if (states.Except(_states).Any() || _states.Except(states).Any())
			{
				var newStates = states.ToArray();
				
				if (!newStates.SequenceEqual(_states))
				{
					_states = newStates;
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}
		}
#endif

		[Serializable]
		public record struct StateRef
		{
			public	String			TypeFullName;
			public 	AssetRef<State>	Ref;

			public override String ToString() => $"{Ref}  {TypeFullName}";
		}
		
		private enum EStateGrabMode : Byte
		{
			NoAutoGrab = 0,
			GrabFromCurrentDir,
			GrabFromCurrentDirAndSubDirs,
		}
	}

	public interface		IOpener : IOpenerB {}
	public interface		IOpenerB { OpenCtx Ctx {get; protected internal set;} }

	public readonly record struct OpenCtx( AssetRef<State> StateRef, FlowNode CallSrc )
	{
		public FlowNode		Open	( object? openParams = null )	=> CallSrc.Graph.Open(StateRef, CallSrc, openParams);
	}
	
#if UNITY_EDITOR
	[UnityEditor.CustomEditor(typeof(FlowLibrary))]
	public class FlowLibraryEditor : UnityEditor.Editor
	{
		private void OnEnable()
		{
			foreach (var t in targets)
			{
				var lib = (FlowLibrary)t;
				lib.RevalidateStates();
			}
		}
	}
	
	[UnityEditor.CustomPropertyDrawer(typeof(FlowLibrary.StateRef))]
	public class StateRefDrawer : UnityEditor.PropertyDrawer
	{
		public override void 	OnGUI	( Rect position, UnityEditor.SerializedProperty property, GUIContent label )
		{
			var refProp		= property.FindPropertyRelative( "Ref" );
			var guidProp	= refProp.FindPropertyRelative( "_uid" );

			var refPos = position;
			refPos.xMax -= 70;

			UnityEditor.EditorGUI.PropertyField(refPos, refProp, GUIContent.none);

			var guidPos = position;
			guidPos.xMin = guidPos.xMax-65;

			GUI.enabled = false;
			UnityEditor.EditorGUI.TextField(guidPos, guidProp.hash128Value.ToString());
			GUI.enabled = true;
		}
	}
#endif
}