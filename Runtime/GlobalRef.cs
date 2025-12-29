using System.Linq;
using System.Runtime.CompilerServices;

namespace Flexy.GameFlow
{
	[ExecuteAlways]
	public class GlobalRef : MonoBehaviour
	{
		[SerializeField] internal Int64 _uid;
		
		public		Int64	Uid			=> _uid;
		
		private		void	Awake		( )		
		{
			GlobalRefs.Set(gameObject.scene, this); 
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
			GlobalRefs.Set(gameObject.scene, this);
			#endif
		}
	}

	public static class GlobalRefs
	{
		[Static(Clear)] static void StaticClear	( ) => _refs = new();
		[Static(Init)]	static void StaticInit	( ) => SceneManager.sceneUnloaded += scene => _refs.Remove(scene);
		
		private static Dictionary<Scene, Dictionary<Int64, GlobalRef>> _refs = new();
		
		public static	void	Set		( Scene scene, GlobalRef gref )						
		{
			if (!_refs.TryGetValue(scene, out var sceneDict))
				sceneDict = _refs[scene] = new();
				
			if (sceneDict.TryGetValue(gref.Uid, out var @ref) && @ref != gref)
			{
#if UNITY_EDITOR			
				if (Application.isPlaying)
					throw new Exception("GlobalRef already exists");
					
				do
				{
					var hash = Hash128.Parse(Guid.NewGuid().ToString());
					gref._uid = Unsafe.As<Hash128, Int64>(ref hash);
				}
				while(gref._uid < 1_000_000_000);
				UnityEditor.EditorUtility.SetDirty(gref);
#else
				throw new Exception("GlobalRef already exists");
#endif
			}
				
			sceneDict[gref.Uid] = gref;
		}
		public static	T		Get<T>	( Scene scene, GlobalRef<T> gref ) where T: UObject	
		{
			return _refs[scene][gref.Uid].GetComponent<T>();
		}
	} 

	[Serializable]
	public record struct GlobalRef<T> where T: UnityEngine.Object
	{
		public	GlobalRef ( Hash128 scene, Int64 uid )	{ _scene = scene; _uid = uid; }
		public	GlobalRef ( String uid )	{ this = default; FromString(uid); }
	
		[SerializeField] Hash128		_scene;
		[SerializeField] Int64			_uid;

		public			SceneRef		Scene		=> new (_scene);
		public			Int64			Uid			=> _uid;
		public			Boolean			IsNone		=> this == default;
		public static	GlobalRef<T>	None		=> default;
	
		public override	String			ToString		( )					=> $"{_scene}[{_uid}]";
		public 			void			FromString		( String address )	=> this = Parse(address);
	
		public			String			ToStringNice	( )					
		{
			return $"{AssetRef.AssetsLoader.GetSceneName(new SceneRef(_scene))}[{_uid}]";
		}
		public static	GlobalRef<T>	Parse			( String address ) 	
		{
			if (String.IsNullOrWhiteSpace( address ))
				return default;

			var uid		= Hash128.Parse( address[..32] ); 
			var subId	= address.Length == 32 ? 0 : Int64.Parse(address[33..^1]);
		
			return new( uid, subId );
		}

		public T Get(Scene scene) => GlobalRefs.Get(scene, this);
	}
}