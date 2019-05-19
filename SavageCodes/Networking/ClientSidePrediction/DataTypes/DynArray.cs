using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace SavageCodes.Networking.ClientSidePrediction
{
	public class DynArray<T> : IEnumerable<T>
	{

		T[] _mem;

		//Constructor determina la capacidad inicial
		public DynArray(int initialCapacity = 1)
		{
			_mem = new T[initialCapacity];
		}

		//Agrega un item al final
		public void Add(T item)
		{

			if (Capacity == Count)
			{
				var tmpNewArray = new T[Count * 2];

				for (int i = 0; i < Count; i++)
				{
					tmpNewArray[i] = _mem[i];
				}

				_mem = tmpNewArray;
			}

			Insert(Count, item);
		}

		public void RemoveLast()
		{
			Count--;
		}

		//Agrega elemento en el indice indicado (el que ocupaba ese indice quedara en el proximo)
		public void Insert(int index, T item)
		{

			if (index >= 0 && index <= Count)
			{
				Count++;
				var tmpFinalArray = new T[Count];

				for (int i = 0; i <= index - 1; i++)
				{
					tmpFinalArray[i] = _mem[i];
				}

				tmpFinalArray[index] = item;

				for (int i = index + 1; i < Count; i++)
				{
					tmpFinalArray[i] = _mem[i - 1];
				}

				for (int i = 0; i < _mem.Length; i++)
				{
					if (i < tmpFinalArray.Length)
						_mem[i] = tmpFinalArray[i];
					else _mem[i] = default(T);
				}

			}

		}

		public T this[int index]
		{
			get { return _mem[index]; }
			set { _mem[index] = value; }
		}

		public int Count { get; private set; }


		public int Capacity
		{
			get { return _mem.Length; }
		}

		public IEnumerator<T> GetEnumerator()
		{

			for (int i = 0; i < Count; i++)
			{
				yield return _mem[i];
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}