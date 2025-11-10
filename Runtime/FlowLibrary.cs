using System.Linq;
using Flexy.AssetRefs.Pipelines;

namespace Flexy.GameFlow
{
    [CreateAssetMenu(fileName = "GameFlow.lib.asset", menuName = "Flexy/GameFlows/Library")]
	public class FlowLibrary : ScriptableObject, IAssetRefsSource
	{
		[SerializeField]	FlowLibrary[]	_dependencies;
		
		[FormerlySerializedAs("_windows")]
		[SerializeField]	StateRef[]				_states;

		private void				OnValidate		( )
		{
			for (var i = 0; i < _states.Length; i++)
				_states[i].TypeFullName = AssetsLoader.EditorLoadAsset( _states[i].Ref )?.GetType( ).FullName;
		}

		[Serializable]
		public record struct StateRef
		{
			public	String				TypeFullName;
			public 	AssetRef<State>		Ref;
			public	Type				Type;

			public override String ToString() => $"{Ref}  {TypeFullName}";
		}

		public List<UnityEngine.Object> CollectAssets( )
		{
			var list = _states.Select( UnityEngine.Object (w) => AssetsLoader.EditorLoadAsset(w.Ref)! ).ToList( );
			
			if (_dependencies != null)
				foreach (var library in _dependencies)
					list.AddRange( library.CollectAssets( ) );

			return list;
		}

		public List<StateRef> CollectStates( )
		{
			var list = new List<StateRef>( );

#if UNITY_EDITOR
			// Actualize TypeFullNames while in editor
			for (var i = 0; i < _states.Length; i++)
				_states[i].TypeFullName = AssetsLoader.EditorLoadAsset( _states[i].Ref )?.GetType( ).FullName;
#endif

			list.AddRange( _states );

			if (_dependencies != null)
				foreach ( var dep in _dependencies )
					if( dep != null )
						list.AddRange( dep.CollectStates( ) );

			return list;
		}
	}

#if UNITY_EDITOR
	[UnityEditor.CustomPropertyDrawer( typeof( FlowLibrary.StateRef ) )]
	public class StateRefDrawer : UnityEditor.PropertyDrawer
	{
		public override void 	OnGUI				( Rect position, UnityEditor.SerializedProperty property, GUIContent label )
		{
			var refProp			= property.FindPropertyRelative( "Ref" );
			var guidProp		= refProp.FindPropertyRelative( "_uid" );

			var refPos = position;
			refPos.xMax -= 70;

			UnityEditor.EditorGUI.PropertyField(refPos, refProp, GUIContent.none);

			var guidPos = position;
			guidPos.xMin = guidPos.xMax-65;

			GUI.enabled = false;
			UnityEditor.EditorGUI.TextField(guidPos, guidProp.hash128Value.ToString()[..7]);
			GUI.enabled = true;
		}
	}
#endif

	public interface		IOpener : IOpenerB {}
	public interface		IOpenerB { OpenCtx Ctx {get; protected  internal set;} }

	public readonly record struct OpenCtx( AssetRef<State> WndRef, State Source )
	{
		public StateHandle Open		( Object openParams = null )	=> Source.GameStage.OpenNewState( WndRef, openParams );
	}
}