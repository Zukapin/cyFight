using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

namespace cyServer
{
    struct PriorityQueue : IComparer<int>
    {
        int Capacity;
        public int Count;
        public float[] Priorities;
        int[] SortedIndexes;
        int SortedUsed;

        public void EnsureCapacity(int NewCapacity)
        {
            if (NewCapacity > Capacity)
            {
                int newCap = Math.Max(Capacity * 2, NewCapacity);
                Array.Resize(ref Priorities, newCap);
                Array.Resize(ref SortedIndexes, newCap);

                Capacity = newCap;
            }
        }

        public void AddIndex(int i)
        {
#if DEBUG
            for (int t = 0; t < Count; t++)
            {
                Debug.Assert(SortedIndexes[t] != i, "Index already exists in this priority queue");
            }
#endif
            Debug.Assert(i >= 0 && i < Capacity, "Index must be greater than 0 and less than capacity" + i + " " + Capacity);
            Debug.Assert(Count < Capacity, "Current count is already at capacity");
            SortedIndexes[Count] = i;
            Priorities[i] = 0;
            Count++;
        }

        public void RemoveIndex(int i)
        {
            Debug.Assert(i >= 0 && i < Capacity);
#if DEBUG
            bool found = false;
#endif
            for (int t = 0; t < Count; t++)
            {
                if (SortedIndexes[t] == i)
                {
                    SortedIndexes[t] = SortedIndexes[Count - 1];
#if DEBUG
                    found = true;
#endif
                    break;
                }
            }

#if DEBUG
            Debug.Assert(found);
#endif
            Count--;
        }

        public void Clear()
        {
            Count = 0;
        }

        public float PeekPrioriy(int i)
        {
            Debug.Assert(SortedUsed == 0);
            Debug.Assert(i < Count);
            return Priorities[SortedIndexes[i]];
        }

        public int Pop()
        {
            Debug.Assert(SortedUsed < Count);
            var toRet = SortedIndexes[SortedUsed++];
            Priorities[toRet] = 0;
            return toRet;
        }

        public void Sort()
        {
            //theoretically could pass in the max number of items we'll ever pull from this list, based on MTU size
            //and then only do a partial sort
            SortedUsed = 0;
            Array.Sort(SortedIndexes, 0, Count, this);
        }

        public int Compare(int x, int y)
        {//don't use this for anything other than the array sort please
            Debug.Assert(x >= 0 && x < Capacity);
            Debug.Assert(y >= 0 && y < Capacity);
            return Math.Sign(Priorities[y] - Priorities[x]);
        }
    }
}
