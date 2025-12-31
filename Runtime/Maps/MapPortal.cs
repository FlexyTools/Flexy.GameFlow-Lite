namespace Flexy.GameFlow.Maps
{
	public class MapPortal : MonoBehaviour
	{
		[SerializeField]	GlobalRef<MapPortal>	_point;
		[SerializeField]	Transform				_enterPlace = null!;

		public	GlobalRef<MapPortal>	Point		=> _point;
		public	Transform				EnterPlace	=> _enterPlace;
	
		[Callable] public void	GoToMapDefault_AtPoint	( ) => this.GetService<Service_MapTransitions>().GoToMap_AtPoint(this, Service_MapTransitions.ELoadMode.Default)	.Forget();
		[Callable] public void	GoToMapSingle_AtPoint	( ) => this.GetService<Service_MapTransitions>().GoToMap_AtPoint(this, Service_MapTransitions.ELoadMode.Single)		.Forget();
		[Callable] public void	GoToMapAdditive_AtPoint	( ) => this.GetService<Service_MapTransitions>().GoToMap_AtPoint(this, Service_MapTransitions.ELoadMode.Additive)	.Forget();
	}
}