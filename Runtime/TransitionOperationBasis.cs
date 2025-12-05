namespace Flexy.GameFlow;

internal static class TransitionOperationBasis
{
	internal static	void		InstantTransition		( FlowNode prevNode, FlowNode nextNode )		
	{
		if (prevNode == nextNode)
			return;
	
		var forwards		= ComputeForwards(prevNode, nextNode);
		var commonParent	= FindNearestCommonParent(prevNode, nextNode);

		var closingBranchNode = prevNode;
		while (closingBranchNode != commonParent)
		{
			try{ closingBranchNode.State.gameObject.SetActive(false);	} catch (Exception ex) { Debug.LogException(ex); }
			try{ NodeStateHide(closingBranchNode, forwards.PrevIsFwd);	} catch (Exception ex) { Debug.LogException(ex); }

			var parent = closingBranchNode.Parent; 
			
			if (!forwards.PrevIsFwd && parent.FirstChild == null && parent.ChildrenShowed)
				parent.State.DoLastChildHide(parent);
				
			closingBranchNode = parent;
		}
		
		var openingBranchNode = commonParent.FirstChild.GetLastSiblingOrNull();
		
		if (openingBranchNode == null)
			nextNode.Graph._mainLineActive = commonParent;
		
		while (openingBranchNode != null)
		{
			nextNode.Graph._mainLineActive = openingBranchNode;
		
			if (forwards.NextIsFwd && !openingBranchNode.Parent.ChildrenShowed)
				openingBranchNode.Parent.State.DoFirstChildShow(openingBranchNode.Parent);
		
			try{ NodeStateShow( openingBranchNode, forwards.NextIsFwd );} catch (Exception ex) { Debug.LogException(ex); }
			try{ openingBranchNode.State.gameObject.SetActive( true );	} catch (Exception ex) { Debug.LogException(ex); }
			
			openingBranchNode = openingBranchNode.FirstChild.GetLastSiblingOrNull();
		}
	}
	
	internal static	Forwards	ComputeForwards			( FlowNode? prev, FlowNode? next )				
	{
		if (prev == null) // We open new separated state
			return new(true, true);
			
		if (next == null) // We close last separated state
			return new(false, false);
	
		for (var iter = next.Back; iter != null; iter = iter.Back)
		{
			if (iter == prev)
				return new(true, true);
		}
		
		for (var iter = prev.Back; iter != null; iter = iter.Back)
		{
			if (iter == next)
				return new(false, false);
		}
		
		return new(false, true);
	}
	internal static	FlowNode	FindNearestCommonParent	( FlowNode? nodeA, FlowNode? nodeB )			
	{
		var aSet = new HashSet<FlowNode>();

		for ( ; nodeA != null; nodeA = nodeA.Parent)
			aSet.Add( nodeA );

		for ( ; nodeB != null; nodeB = nodeB.Parent)
			if (aSet.Contains( nodeB ))
				return nodeB;

		throw new InvalidOperationException("Graph broken, can not find common parent, it must be at least one common parent -> Root of the graph ");
	}
	internal static	void		NodeStateHide			( FlowNode node, Boolean isMoveForward )		
	{
		var state = node.State;
		
		try
		{
			if( isMoveForward )	state.DoFwdHide( );
			else				state.DoHide( );
		}
		catch ( Exception ex ) { Debug.LogException( ex ); }
	}
	internal static	void		NodeStateShow			( FlowNode node, Boolean isMoveForward )		
	{
		var state	= node.State;
		state._node	= node;
			
		if (!node.FullyInited && !isMoveForward)
			try						{ state.DoShow(); }
			catch (Exception ex)	{ Debug.LogException(ex); }
		
		node.FullyInited = true;
		
		try
		{
			if (isMoveForward)	state.DoShow();
			else				state.DoBackShow();
		}
		catch (Exception ex) { Debug.LogException(ex); }
	}
	
	internal record struct Forwards(Boolean PrevIsFwd, Boolean NextIsFwd);
}