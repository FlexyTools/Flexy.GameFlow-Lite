using System.Linq;
using Flexy.AssetRefs.Pipelines;

namespace Flexy.GameFlow
{
    [CreateAssetMenu(fileName = "GameFlow.lib.asset", menuName = "Flexy/GameFlows/Library")]
	public class FlowLibrary : ScriptableObject, IAssetRefsSource
	{
		[SerializeField]	EStateGrabMode	_autoGrabMode	= EStateGrabMode.GrabFromCurrentDir;
		[SerializeField]	FlowLibrary[]?	_dependencies	= null;
		[SerializeField]	StateRef[]		_states			= null!;

		public	List<UObject>	CollectAssets	( )		
		{
			var list = _states.Select( UObject(w) => AssetsLoader.EditorLoadAsset(w.Ref)! ).ToList();
			
			if (_dependencies != null)
				foreach (var library in _dependencies)
					list.AddRange( library.CollectAssets() );

			return list;
		}
		public	List<StateRef>	CollectStates	( )		
		{
			var list = new List<StateRef>();

			#if UNITY_EDITOR
			OnValidate();
			#endif

			list.AddRange(_states);

			if (_dependencies != null)
				foreach (var dep in _dependencies)
					if (dep != null)
						list.AddRange( dep.CollectStates() );

			return list;
		}

#if UNITY_EDITOR
		private	void			OnValidate		( )		
		{
			if (_autoGrabMode is not EStateGrabMode.NoGrab)
			{
				EditorGrabStates(_autoGrabMode);
			}
			else
			{
				for (var i = 0; i < _states.Length; i++)
				{
					var newName = AssetsLoader.EditorLoadAsset( _states[i].Ref )?.GetType().FullName ?? "";
					if (_states[i].TypeFullName != newName)
					{
						_states[i].TypeFullName = newName; 
						UnityEditor.EditorUtility.SetDirty(this);
					}
				}
			}
		}
		
		[ContextMenu("Revalidate")]
		internal	void	RevalidateStates					( )			
		{
			OnValidate();
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
			if (grabMode is EStateGrabMode.NoGrab)
				return;
		
			var path	= UnityEditor.AssetDatabase.GetAssetPath(this);
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
						TypeFullName = state.GetType().FullName ?? ""
					});
				}
			}

			if (states.Except(_states).Any() || _states.Except(states).Any())
			{
				_states = states.ToArray();
				UnityEditor.EditorUtility.SetDirty(this);
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
			NoGrab = 0,
			GrabFromCurrentDir,
			GrabFromCurrentDirAndSubDirs,
		}
	}

	public interface		IOpener : IOpenerB {}
	public interface		IOpenerB { OpenCtx Ctx {get; protected internal set;} }

	public readonly record struct OpenCtx( AssetRef<State> StateRef, State CallSrc )
	{
		public StateHandle Open		( Object? openParams = null )	=> CallSrc.Node.Graph.Open(StateRef, CallSrc, openParams);
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