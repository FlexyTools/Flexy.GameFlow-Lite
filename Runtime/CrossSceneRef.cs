using System.Linq;
using System.Runtime.CompilerServices;

namespace Flexy.GameFlow
{
	[ExecuteAlways]
	public class CrossSceneRef : MonoBehaviour
	{
		[SerializeField] internal Int64 _uid;
		
		public		Int64	Uid			=> _uid;
		
		private		void	Awake		( )		
		{
			CrossSceneRefs.Set(gameObject.scene, this); 
		}
		private		void	OnValidate	( )		
		{
			#if UNITY_EDITOR
			if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) && _uid != default)
			{
				_uid = default;
				UnityEditor.EditorUtility.SetDirty(this);
			}
			
			else if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
			{
				var overrides = UnityEditor.PrefabUtility.GetPropertyModifications(this); 
				if (_uid == 0 || overrides == null || overrides.All(p => p.propertyPath != "_uid")) 
				{
					do
					{
						var hash = Hash128.Parse(Guid.NewGuid().ToString());
						_uid = Unsafe.As<Hash128, Int64>(ref hash);
					}
					while(_uid < 1_000_000_000);
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}
			CrossSceneRefs.Set(gameObject.scene, this);
			#endif
		}
	}

	public static class CrossSceneRefs
	{
		[Static(Clear)] static void StaticClear	( ) => _refs = new();
		[Static(Init)]	static void StaticInit	( ) => SceneManager.sceneUnloaded += scene => _refs.Remove(scene);
		
		private static Dictionary<Scene, Dictionary<Int64, CrossSceneRef>> _refs = new();
		
		public static	void	Set		( Scene scene, CrossSceneRef csref )						
		{
			if (!_refs.TryGetValue(scene, out var sceneDict))
				sceneDict = _refs[scene] = new();
				
			if (sceneDict.TryGetValue(csref.Uid, out var @ref) && @ref != csref)
			{
#if UNITY_EDITOR			
				if (Application.isPlaying)
					throw new Exception("GlobalRef already exists");
					
				do
				{
					var hash = Hash128.Parse(Guid.NewGuid().ToString());
					csref._uid = Unsafe.As<Hash128, Int64>(ref hash);
				}
				while(csref._uid < 1_000_000_000);
				UnityEditor.EditorUtility.SetDirty(csref);
#else
				throw new Exception("GlobalRef already exists");
#endif
			}
				
			sceneDict[csref.Uid] = csref;
		}
		public static	T		Get<T>	( Scene scene, CrossSceneRef<T> csref ) where T: Object	
		{
			return _refs[scene][csref.Uid].GetComponent<T>();
		}
	} 

	[Serializable]
	public record struct CrossSceneRef<T> where T: UnityEngine.Object
	{
		public	CrossSceneRef ( Hash128 scene, Int64 uid )	{ _scene = scene; _uid = uid; }
		public	CrossSceneRef ( String uid )				{ this = default; FromString(uid); }
	
		[SerializeField] Hash128		_scene;
		[SerializeField] Int64			_uid;

		public			SceneRef		Scene		=> new (_scene);
		public			Int64			Uid			=> _uid;
		public			Boolean			IsNone		=> this == default;
		public static	CrossSceneRef<T>None		=> default;
	
		public override	String			ToString		( )					=> $"{_scene}[{_uid}]";
		public 			void			FromString		( String address )	=> this = Parse(address);
	
		public			String			ToStringNice	( )					
		{
			return $"{SceneRef.SceneLoader.GetSceneName(new SceneRef(_scene))}[{_uid}]";
		}
		public static	CrossSceneRef<T>Parse			( String address ) 	
		{
			if (String.IsNullOrWhiteSpace( address ))
				return default;

			var uid		= Hash128.Parse( address[..32] ); 
			var subId	= Int64.Parse( address[33..^1] );
		
			return new( uid, subId );
		}

		public T Get(Scene scene) => CrossSceneRefs.Get(scene, this);
	}
}