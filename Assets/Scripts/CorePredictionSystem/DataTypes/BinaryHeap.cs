using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BinaryHeap<T>
{
	private DynArray<HeapElement<T>> _data = new DynArray<HeapElement<T>>(2);

	int Parent(int position) { return (position - 1) / 2; }

	int LeftChild(int position) { return 2 * position + 1; }

	int RightChild(int position) { return 2 * position + 2; }
	
	public int Count { get { return _data.Count; } }
	
	void Swap(int position1, int position2)
	{
		HeapElement<T> temp1 = _data[position1];
		HeapElement<T> temp2 = _data[position2];

		_data[position1] = temp2;
		_data[position2] = temp1;
	}
	
	void MoveUp(int position)
	{
		while ((position > 0) && (_data[Parent(position)].Weight > _data[position].Weight))
		{
			int original_parent_pos = Parent(position);
			Swap(position, original_parent_pos);
			position = original_parent_pos;
		}
	}

	void MoveDown(int position) 
	{
		int lchild = LeftChild(position);
		int rchild = RightChild(position);
		
		int largest = 0;
		if ((lchild < Count) && (_data[lchild].Weight < _data[position].Weight))
		{
			largest = lchild;
		}
		else
		{
			largest = position;
		}
		if ((rchild < Count) && (_data[rchild].Weight < _data[largest].Weight))
		{
			largest = rchild;
		}
		
		if (largest != position)
		{
			Swap(position,largest);
			MoveDown(largest);
		}
	}
	
	public HeapElement<T> Dequeue() 
	{
		HeapElement<T> minNode = _data[0];
		Swap(0, Count - 1);
		_data.RemoveLast();
		MoveDown(0);
		return minNode;
	}

	public void Enqueue(HeapElement<T> element) {
		
		_data.Add(element);
		MoveUp(_data.Count-1);
	}

    public HeapElement<T> Peek()
    {
        return _data[0];
    }


    public bool IsEmpty { get { return _data.Count == 0; } }
}

public class HeapElement<T>
{
	private T _element;
	private float _weight; 

	public T Element { get { return _element; } }
	public float Weight { get { return _weight; } }

	public HeapElement(T element, float weight) {
		_element = element;
		_weight = weight;
	}
	
	public override bool Equals(object obj)
	{
		var other = obj as HeapElement<T>;

		var result =
			other != null
			&& other.Element.Equals(_element) 
			&& other.Weight == _weight;
		
		return result;
	}
}