using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary
{
    //An indexable queue supporting insertion whilst storing in array format
    class IndexableQueue<T>
    {
        T[] _queue;
        int _zeroPtr;
        int _length;
        int _size;
        public int MaxSize { get { return _size; } }
        public int Length
        {
            get { return _length; }
        }

        public IndexableQueue(int size)
        {
            _queue = new T[size];
            this._size = size;
            _length = 0;
            _zeroPtr = 0;
        }

        //Returns the element at the given index
        public T GetAt(int index)
        {
            if (index >= _length || index < 0)
            {
                throw new IndexOutOfRangeException();
            }
            return _queue[AdjustIndex(index)];
        }

        //Inserts value at index by shifting up other elements
        public void InsertAt(int index, T val)
        {
            if (_length == _size)
                throw new ExceptionExtensions.QueueFullException();

            if (index > _length || index < 0)
                throw new IndexOutOfRangeException();

            if (index > _length / 2)
            {
                //Shift back elements
                for (int i = _length; i > index; i--)
                {
                    _queue[AdjustIndex(i)] = _queue[AdjustIndex(i - 1)];
                }
            }
            else
            {
                //Shift forward elements
                _zeroPtr = AdjustIndex(-1);
                for (int i = 0; i < index; i--)
                {
                    _queue[AdjustIndex(i)] = _queue[AdjustIndex(i + 1)];
                }

            }
            _queue[AdjustIndex(index)] = val;
            _length++;
        }

        //Returns and removes the first element in the queue
        public T PopFront()
        {
            if (_length > 0)
            {
                T rtn = _queue[_zeroPtr];
                _zeroPtr = AdjustIndex(1);
                _length--;
                return rtn;
            }
            else
                throw new ExceptionExtensions.QueueEmptyException();
        }


        int AdjustIndex(int index)
        {
            return (_zeroPtr + index) % _size;
        }

    }
}
