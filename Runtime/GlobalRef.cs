using System.Linq;
using UnityEditor;

namespace Flexy.GameFlow
{
	[ExecuteAlways]
	public class GlobalRef : MonoBehaviour
	{
		[Static(Clear)] static void StaticClear	( ) => _refs = new();
		[Static(Init)]	static void StaticInit	( ) => SceneManager.sceneUnloaded += (scene) => _refs.Remove(scene.name);
		
		private static Dictionary<String, Dictionary<Hash128, GlobalRef>> _refs = new( ); 
	
		[SerializeField] Hash128 _uid;
		
		public		Hash128	Uid => _uid;

		private		void	Awake		( )		
		{
			if (!_refs.TryGetValue(gameObject.scene.name, out var sceneDict))
				sceneDict = _refs[gameObject.scene.name] = new();
				
			if (sceneDict.TryGetValue(_uid, out var @ref) && @ref != this)
			{
				_uid = Hash128.Parse(Guid.NewGuid().ToString());
				EditorUtility.SetDirty(this);
			}
				
			sceneDict[_uid] = this; 
		}
		private		void	OnValidate	( )		
		{
			if (PrefabUtility.IsPartOfPrefabAsset(this) && _uid != default)
			{
				_uid = default;
				EditorUtility.SetDirty(this);
			}
			
			else if (PrefabUtility.IsPartOfPrefabInstance(this))
			{
				var overrides = PrefabUtility.GetPropertyModifications(this);
				if (overrides == null || overrides.All(p => p.propertyPath != "_uid"))
				{
					_uid = Hash128.Parse(Guid.NewGuid().ToString());
					EditorUtility.SetDirty(this);
				}
			}
		}
	}

	[Serializable]
	public record struct GlobalRef<T> : IRefLike where T: UnityEngine.Object
	{
		public	GlobalRef ( Hash128 uid )	{ _uid = uid; }
		public	GlobalRef ( String uid )	{ this = default; FromString(uid); }
	
		[SerializeField] Hash128		_uid;
	
		public			Hash128			Uid			=> _uid;
		public			Boolean			IsNone		=> this == default;
		public static	GlobalRef<T>	None		=> default;
	
		public override	String			ToString		( )					=> _uid.ToString();
		public 			void			FromString		( String address )	=> this = Parse(address);
	
		public static	GlobalRef<T>	Parse			( String address ) 	
		{
			if (String.IsNullOrWhiteSpace( address ))
				return default;

			return new(Hash128.Parse(address));
		}
	}
}