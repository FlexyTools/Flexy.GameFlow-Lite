namespace Flexy.GameFlow.Maps
{
	public class Service_MapTransitions : MonoBehaviour
	{
		[SerializeField]	CanvasGroup		_backOverlay	= null!;
		[SerializeField]	String?			_playerMobTag;
		[SerializeField]	ELoadMode		_defaultLoadMode;
	
		private GameObject?	_persistedPlayerMob;
	
		public virtual async	UniTask				GoToMap_AtPoint			( MapPortal portal, ELoadMode loadMode = default, GameObject? ctx = null )	
		{
			if (loadMode == default)
				loadMode = _defaultLoadMode;
		
			Debug.Log( $"[Service_SimpleScene] GoToMap_AtPoint {loadMode} {portal.Point.ToStringNice()}" );
			
			await FadeOut();
			await StorePersistentObjects();
			var enterPoint = await LoadMap(portal, loadMode, ctx != null ? ctx : gameObject);
			await RestorePersistentObjects(enterPoint);
			await FadeIn();
		}
	
		public virtual async	UniTask				FadeOut					( )							
		{
			_backOverlay.alpha	= 0.0f;
			_backOverlay.gameObject.SetActive(true);
			
			while (_backOverlay.alpha < 1.0f)
			{
				_backOverlay.alpha	+= Time.deltaTime * 2;
				await UniTask.NextFrame();
			}
		}
		public virtual async	UniTask				StorePersistentObjects	( )							
		{
			_persistedPlayerMob = String.IsNullOrWhiteSpace(_playerMobTag) ? null : GameObject.FindWithTag(_playerMobTag);
			
			if (_persistedPlayerMob != null)
				_persistedPlayerMob.SetActive(false);	
		}
		public virtual async	UniTask<MapPortal>	LoadMap					( MapPortal portal, ELoadMode loadMode, GameObject ctx )	
		{
			MapPortal resultPoint;
		
			if (loadMode == default)
				loadMode = ELoadMode.Additive;
		
			if (loadMode == ELoadMode.Single)
			{
				await SceneRef.SceneLoader.LoadDummySceneAsync(ctx, LoadSceneMode.Single);
				
				var nextScene	= await portal.Point.Scene.LoadSceneAsync(ctx, LoadSceneMode.Single);
				resultPoint		= portal.Point.Get(nextScene);
			}
			else
			{
				var currentScene = SceneManager.GetActiveScene();
				
				var dummy = await SceneRef.SceneLoader.LoadDummySceneAsync(ctx, LoadSceneMode.Additive);
				
				if (loadMode != ELoadMode.AdditiveNoUnload)
					await SceneManager.UnloadSceneAsync(currentScene);
				
				var preloadedScene = SceneManager.GetSceneByName(SceneRef.SceneLoader.GetSceneName(portal.Point.Scene)); 
				if (preloadedScene.IsValid())
				{
					resultPoint		= portal.Point.Get(preloadedScene);
				}
				else
				{
					var nextScene	= await portal.Point.Scene.LoadSceneAsync(ctx, LoadSceneMode.Additive);
					resultPoint		= portal.Point.Get(nextScene);
				}
				
				await SceneManager.UnloadSceneAsync(dummy);
			}
			
			return resultPoint;
		}
		public virtual async	UniTask				RestorePersistentObjects( MapPortal enterPoint )	
		{
			if (_persistedPlayerMob != null)
			{
				var playerMob = _persistedPlayerMob;
				_persistedPlayerMob = null;
				
				playerMob.transform.SetLocalPositionAndRotation(enterPoint.EnterPlace.position, enterPoint.EnterPlace.rotation);
				playerMob.gameObject.SetActive(true);
			}
		}
		public virtual async	UniTask				FadeIn					( )							
		{
			while (_backOverlay.alpha > 0.0f)
			{
				_backOverlay.alpha	-= Time.deltaTime;
				await UniTask.NextFrame();
			}

			_backOverlay.alpha	= 0.0f;
			_backOverlay.gameObject.SetActive(false);
		}
		
		public virtual LoadSceneTask		PreloadMap	( SceneRef scene, GameObject ctx )							
		{
			return scene.LoadSceneAsync(ctx, LoadSceneMode.Additive);
		}
		public virtual AsyncOperation		UnloadMap	( SceneRef scene, UnloadSceneOptions options = default )	
		{
			return SceneManager.UnloadSceneAsync(SceneRef.SceneLoader.GetSceneName(scene), options);
		} 
		
		public enum ELoadMode : Byte
		{
			Default,
			Single,
			Additive,
			AdditiveNoUnload,
		}
	}
}