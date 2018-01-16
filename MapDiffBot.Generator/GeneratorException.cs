using System;
using System.Runtime.Serialization;

namespace MapDiffBot.Generator
{
	/// <summary>
	/// The <see cref="Exception"/> type thrown by <see cref="IGenerator"/>s
	/// </summary>
	[Serializable]
	public sealed class GeneratorException : Exception
	{
		/// <summary>
		/// Construct a <see cref="GeneratorException"/>
		/// </summary>
		public GeneratorException() { }

		/// <summary>
		/// Construct a <see cref="GeneratorException"/>
		/// </summary>
		/// <param name="message">A message about the exception</param>
		public GeneratorException(string message) : base(message) { }

		/// <summary>
		/// Construct a <see cref="GeneratorException"/>
		/// </summary>
		/// <param name="message">A message about the exception</param>
		/// <param name="innerException">The <see cref="Exception"/> that caused this to be thrown</param>
		public GeneratorException(string message, Exception innerException) : base(message, innerException) { }

		/// <summary>
		/// Construct a <see cref="GeneratorException"/>
		/// </summary>
		/// <param name="serializationInfo">The <see cref="SerializationInfo"/> to use</param>
		/// <param name="streamingContext">The <see cref="StreamingContext"/> to use</param>
		GeneratorException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
	}
}
