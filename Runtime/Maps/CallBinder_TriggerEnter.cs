namespace Flexy.GameFlow.Maps
{
	public class CallBinder_TriggerEnter : CallBinder
	{
		[SerializeField]	String?		_colliderTag;
		
		private		Action?	_action;

		private		void	Awake			( )					
		{
			Init( ref _action );	
		}
		private		void	OnTriggerEnter	( Collider other )	
		{
			if (other.CompareTag(_colliderTag))
				_action?.Invoke();
		}
	}
}