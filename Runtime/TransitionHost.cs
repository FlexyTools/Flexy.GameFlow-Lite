namespace Flexy.GameFlow;

public class TransitionHost
{
	internal	FlowNode		_node		= null!;
	internal	FlowNode		_tipNode	= null!;
	internal	FlowNode		_activeNode	= null!;
	private		Boolean			_doTransition;
	
	public		FlowNode		TipNode		=> _tipNode;
	public		FlowNode		ActiveNode	=> _activeNode;
	
	public		FlowNode		TryGoBack						( )		
	{
		if (_tipNode.Back != null && _tipNode.State.TryGoBack())
			_node.Graph.RemoveNodesUpTo(_tipNode, _tipNode.Back);

		return _tipNode;
	}
	public			void		TransitionNow					( )		
	{
		if (!_doTransition) 
			return;
		
		_doTransition = false;
		DoStateTransitions();
	}
	
	internal		void		ScheduleSwitchStates			( )		
	{
		_doTransition	= true;
	}
	internal async	UniTask		SwitchStatesAsyncInfiniteLoop	( )		
	{
		while (Application.isPlaying && _node.State)
		{
			// Change view at last update (before animations)
			// This will allow to make many changes in update and then only one view transition
			await UniTask.NextFrame( PlayerLoopTiming.LastUpdate );

			if (!_doTransition)
				continue;

			_doTransition	= false;

			try						{ DoStateTransitions(); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
		}
	}
	internal		void		DoStateTransitions				( )		
	{
		if (_activeNode == _tipNode)
			return;
	
		InstantTransition(_activeNode, _tipNode);
		
		_activeNode = _tipNode;
	}
	
	internal 		void		NodeStateHide			( FlowNode node, Boolean isForwardHide )	
	{
		var state			= node.State;

		//Debug.Log( $"[TransitionHost] {node} - {(isForwardHide? "Forward Hide": "Hide")}" );
		
		try
		{
			if( isForwardHide )	state.DoForwardHide( );
			else				state.DoHide( );
		}
		catch ( Exception ex ) { Debug.LogException( ex ); }
	}
	internal 		void		NodeStateShow			( FlowNode node, Boolean isBackShow )		
	{
		var state	= node.State;
		state._node	= node;
		
		//Debug.Log( $"[TransitionHost] {node} - {(isBackShow? "Back Show": "Show")}" );
		
		if (!node.IsShowed && isBackShow)
			try						{ state.DoShow(); }
			catch (Exception ex)	{ Debug.LogException(ex); }
		
		node.IsShowed = true;
		
		try
		{
			if (isBackShow)	state.DoBackShow();
			else			state.DoShow	();
		}
		catch (Exception ex) { Debug.LogException(ex); }
	}

	private			void		InstantTransition		( FlowNode prevNode, FlowNode nextNode )	
	{
		if (prevNode == nextNode)
			return;
	
		var commonParent	= FlowNodeExt.FindNearestCommonParent(prevNode, nextNode);

		var closingBranchNode = prevNode;
		while (closingBranchNode != commonParent)
		{
			var isForwardHide	= closingBranchNode.IsOpened;
		
			try{ closingBranchNode.State.gameObject.SetActive(false);	} catch (Exception ex) { Debug.LogException(ex); }
			try{ NodeStateHide(closingBranchNode, isForwardHide);		} catch (Exception ex) { Debug.LogException(ex); }

			var parent = closingBranchNode.Parent; 
			
			if (!isForwardHide && parent.FirstChild == null && parent.ChildrenShowed)
				parent.State.DoLastChildHide(parent);
				
			closingBranchNode = parent;
		}
		
		var openingBranchNode = commonParent.FirstChild.GetLastSiblingOrNull();
		
		while (openingBranchNode != null)
		{
			var isBackShow = _activeNode.IsInForwardOf(openingBranchNode);
		
			if (!isBackShow && !openingBranchNode.Parent.ChildrenShowed)
				openingBranchNode.Parent.State.DoFirstChildShow(openingBranchNode.Parent);
		
			try{ NodeStateShow( openingBranchNode, isBackShow );		} catch (Exception ex) { Debug.LogException(ex); }
			try{ openingBranchNode.State.gameObject.SetActive( true );	} catch (Exception ex) { Debug.LogException(ex); }
			
			openingBranchNode = openingBranchNode.FirstChild.GetLastSiblingOrNull();
		}
	}
}