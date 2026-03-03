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
		private		void	OnDestroy	( )		
		{
			CrossSceneRefs.Remove(gameObject.scene, this);
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
		[Static(Clear)] static void StaticClear	( ) {_refsSn = new(); _refsSr = new();}
		[Static(Init)]	static void StaticInit	( ) => SceneManager.sceneUnloaded += delegate (Scene scene){ _refsSn.Remove(scene); _refsSr.Remove(scene.GetRef()); };
		
		private static Dictionary<Scene,	Dictionary<Int64, CrossSceneRef>> _refsSn = new();
		private static Dictionary<SceneRef,	Dictionary<Int64, CrossSceneRef>> _refsSr = new();
		
		public static	T		Get<T>	( Scene scene, CrossSceneRef<T> csref ) where T: Object		
		{
			if (!_refsSn.TryGetValue(scene, out var sceneDict))
				_refsSr.TryGetValue(csref.Scene, out sceneDict);
				
			return sceneDict[csref.Uid].GetComponent<T>();
		}
		public static	T?		Find<T>	( Scene scene, CrossSceneRef<T> csref ) where T: Object		
		{
			if (!_refsSn.TryGetValue(scene, out var sceneDict) && !_refsSr.TryGetValue(csref.Scene, out sceneDict))
				return null;
				
			return sceneDict.TryGetValue(csref.Uid, out var @ref) ? @ref.GetComponent<T>() : null;
		}
		
		internal static	void	Set		( Scene scene, CrossSceneRef csref )	
		{
			if (scene == default)
				return;
		
			if (!_refsSn.TryGetValue(scene, out var sceneDict))
			{
				sceneDict = _refsSn[scene] = new();
				_refsSr[scene.GetRef()] = sceneDict;
			}
				
			if (sceneDict.TryGetValue(csref.Uid, out var @ref) && @ref != csref)
			{
#if UNITY_EDITOR			
				if (Application.isPlaying)
					throw new Exception("CrossSceneRef already exists");
					
				do
				{
					var hash = Hash128.Parse(Guid.NewGuid().ToString());
					csref._uid = Unsafe.As<Hash128, Int64>(ref hash);
				}
				while(csref._uid < 1_000_000_000);
				UnityEditor.EditorUtility.SetDirty(csref);
#else
				throw new Exception("CrossSceneRef already exists");
#endif
			}
				
			sceneDict[csref.Uid] = csref;
		}
		internal static	void	Remove	( Scene scene, CrossSceneRef csref )	
		{
			if (!_refsSn.TryGetValue(scene, out var sceneDict))
				return;
				
			if (sceneDict.TryGetValue(csref.Uid, out var @ref) && @ref != csref)
			{
				if (Application.isPlaying)
					throw new Exception($"CrossSceneRef broken somehow 2 refs with the same uid, {@ref} and {csref}");
			}
				
			sceneDict.Remove(csref.Uid);
		}
	} 

	[Serializable]
	public record struct CrossSceneRef<T> where T: Object
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

		public	T	Get		( Scene scene = default ) => CrossSceneRefs.Get	(scene, this);
		public	T?	Find	( Scene scene = default ) => CrossSceneRefs.Find(scene, this);
	}
}