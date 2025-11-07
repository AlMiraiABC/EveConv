using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EveConv.Abstraction.Cache
{
    /// <summary>
    /// Provides list-based cache operations for managing ordered collections with support for operations from both ends.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in the list cache.</typeparam>
    public interface IListCache<TValue>
    {
        #region Push Operations

        /// <summary>
        /// Pushes a value to the left (head) of the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="value">The value to push to the list.</param>
        /// <param name="ttl">Optional time-to-live for the list. If null, the list will not expire automatically.</param>
        /// <returns>A task containing the length of the list after the push operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        virtual Task<int> LeftPushAsync(string key, TValue value, TimeSpan? ttl = null)
        {
            return LeftPushAsync(key, [value], ttl);
        }

        /// <summary>
        /// Pushes multiple values to the left (head) of the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="values">The values to push to the list.</param>
        /// <param name="ttl">Optional time-to-live for the list. If null, the list will not expire automatically.</param>
        /// <returns>A task containing the length of the list after the push operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key or values is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        Task<int> LeftPushAsync(string key, IEnumerable<TValue> values, TimeSpan? ttl = null);

        /// <summary>
        /// Pushes a value to the right (tail) of the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="value">The value to push to the list.</param>
        /// <param name="ttl">Optional time-to-live for the list. If null, the list will not expire automatically.</param>
        /// <returns>A task containing the length of the list after the push operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        virtual Task<int> RightPushAsync(string key, TValue value, TimeSpan? ttl = null)
        {
            return RightPushAsync(key, [value], ttl);
        }

        /// <summary>
        /// Pushes multiple values to the right (tail) of the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="values">The values to push to the list.</param>
        /// <param name="ttl">Optional time-to-live for the list. If null, the list will not expire automatically.</param>
        /// <returns>A task containing the length of the list after the push operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key or values is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        Task<int> RightPushAsync(string key, IEnumerable<TValue> values, TimeSpan? ttl = null);

        #endregion

        #region Pop Operations

        /// <summary>
        /// Removes and returns the first element from the left (head) of the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <returns>A task containing the popped value if the list exists and is not empty, or null if the key does not exist or the list is empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        virtual async Task<TValue?> LeftPopAsync(string key)
        {
            var removed = await LeftPopAsync(key, 1);
            if (removed.Count == 0)
            {
                return default;
            }
            return removed[0];
        }

        /// <summary>
        /// Removes and returns multiple elements from the left (head) of the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="count">The number of elements to pop from the list.</param>
        /// <returns>A task containing a list of popped values. The list may contain fewer elements if the list has fewer items than requested.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters, or count is less than 1.</exception>
        Task<IList<TValue>> LeftPopAsync(string key, int count);

        /// <summary>
        /// Removes and returns the last element from the right (tail) of the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <returns>A task containing the popped value if the list exists and is not empty, or null if the key does not exist or the list is empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        virtual async Task<TValue?> RightPopAsync(string key)
        {
            var removed = await RightPopAsync(key, 1);
            if (removed.Count == 0)
            {
                return default;
            }
            return removed[0];
        }

        /// <summary>
        /// Removes and returns multiple elements from the right (tail) of the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="count">The number of elements to pop from the list.</param>
        /// <returns>A task containing a list of popped values. The list may contain fewer elements if the list has fewer items than requested.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters, or count is less than 1.</exception>
        Task<IList<TValue>> RightPopAsync(string key, int count);

        #endregion

        #region Get Operations

        /// <summary>
        /// Gets the element at the specified index in the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="index">The zero-based index of the element to retrieve. Negative indices count from the end (-1 is the last element).</param>
        /// <returns>A task containing the value at the specified index, or null if the key does not exist or the index is out of range.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        Task<TValue?> GetByIndexAsync(string key, int index);

        /// <summary>
        /// Gets a range of elements from the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="start">The starting index (inclusive). Negative indices count from the end.</param>
        /// <param name="stop">The ending index (inclusive). Negative indices count from the end.</param>
        /// <returns>A task containing a list of values in the specified range. Returns an empty list if the key does not exist.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        /// <remarks>
        /// Both start and stop are inclusive indices. Use 0 for the first element and -1 for the last element.
        /// GetRangeAsync("mylist", 0, -1) returns all elements in the list.
        /// </remarks>
        Task<IList<TValue>> GetRangeAsync(string key, int start = 0, int stop = -1);

        /// <summary>
        /// Gets the length of the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <returns>A task containing the length of the list, or 0 if the key does not exist.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        Task<int> GetLengthAsync(string key);

        #endregion

        #region Modify Operations

        /// <summary>
        /// Sets the value of an element at the specified index in the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="index">The zero-based index of the element to set. Negative indices count from the end (-1 is the last element).</param>
        /// <param name="value">The new value to set at the specified index.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false if the key doesn't exist or index is out of range.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        Task<bool> SetByIndexAsync(string key, int index, TValue value);

        /// <summary>
        /// Trims the list stored at the specified key to contain only the elements in the specified range.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="start">The starting index (inclusive) of the range to keep. Negative indices count from the end.</param>
        /// <param name="stop">The ending index (inclusive) of the range to keep. Negative indices count from the end.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        /// <remarks>
        /// Elements outside the specified range will be removed.
        /// TrimAsync("mylist", 0, 2) keeps only the first three elements.
        /// </remarks>
        Task TrimAsync(string key, int start, int stop);

        /// <summary>
        /// Removes the first count occurrences of elements equal to value from the list stored at the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached list. Cannot be null or empty.</param>
        /// <param name="value">The value to remove from the list.</param>
        /// <param name="count">The number of occurrences to remove. If positive, removes from head to tail. If negative, removes from tail to head. If zero, removes all occurrences.</param>
        /// <returns>A task containing the number of elements removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        Task<int> RemoveAsync(string key, TValue value, int count = 0);

        #endregion

        #region Utility Operations

        #endregion
    }
}
