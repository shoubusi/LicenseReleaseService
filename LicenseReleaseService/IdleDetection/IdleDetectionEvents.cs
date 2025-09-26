using System;
using System.Collections.Generic;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Event arguments for idle detection events
    /// </summary>
    public class IdleDetectionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the session identifier
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// Gets the process identifier
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Gets the user name associated with the session
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// Gets the computer name associated with the session
        /// </summary>
        public string ComputerName { get; }

        /// <summary>
        /// Gets the detection timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the confidence level of the detection (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; }

        /// <summary>
        /// Gets the idle time duration
        /// </summary>
        public TimeSpan IdleTime { get; }

        /// <summary>
        /// Gets the detection method or strategy used
        /// </summary>
        public string DetectionMethod { get; }

        /// <summary>
        /// Gets additional metadata about the detection
        /// </summary>
        public Dictionary<string, object> Metadata { get; }

        /// <summary>
        /// Gets the reason for the detection
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the IdleDetectionEventArgs class
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <param name="processId">The process identifier</param>
        /// <param name="userName">The user name</param>
        /// <param name="computerName">The computer name</param>
        /// <param name="confidence">The confidence level</param>
        /// <param name="idleTime">The idle time duration</param>
        /// <param name="detectionMethod">The detection method</param>
        /// <param name="reason">The reason for detection</param>
        public IdleDetectionEventArgs(
            string sessionId,
            int processId,
            string userName,
            string computerName,
            double confidence,
            TimeSpan idleTime,
            string detectionMethod,
            string reason)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            ProcessId = processId;
            UserName = userName ?? throw new ArgumentNullException(nameof(userName));
            ComputerName = computerName ?? throw new ArgumentNullException(nameof(computerName));
            Confidence = confidence;
            IdleTime = idleTime;
            DetectionMethod = detectionMethod ?? throw new ArgumentNullException(nameof(detectionMethod));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            Timestamp = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
        }

        /// <summary>
        /// Adds metadata to the event arguments
        /// </summary>
        /// <param name="key">The metadata key</param>
        /// <param name="value">The metadata value</param>
        public void AddMetadata(string key, object value)
        {
            Metadata[key] = value;
        }

        /// <summary>
        /// Returns a string representation of the idle detection event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"IdleDetection: Session={SessionId}, Process={ProcessId}, User={UserName}, " +
                   $"Computer={ComputerName}, Confidence={confidence:F2}, IdleTime={IdleTime.TotalMinutes:F1}m, " +
                   $"Method={DetectionMethod}, Reason={Reason}";
        }
    }

    /// <summary>
    /// Event arguments for session state change events
    /// </summary>
    public class SessionStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the session identifier
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// Gets the process identifier
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Gets the previous session state
        /// </summary>
        public SessionState PreviousState { get; }

        /// <summary>
        /// Gets the new session state
        /// </summary>
        public SessionState NewState { get; }

        /// <summary>
        /// Gets the state change timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the reason for the state change
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets additional context about the state change
        /// </summary>
        public Dictionary<string, object> Context { get; }

        /// <summary>
        /// Initializes a new instance of the SessionStateChangedEventArgs class
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <param name="processId">The process identifier</param>
        /// <param name="previousState">The previous session state</param>
        /// <param name="newState">The new session state</param>
        /// <param name="reason">The reason for the state change</param>
        public SessionStateChangedEventArgs(
            string sessionId,
            int processId,
            SessionState previousState,
            SessionState newState,
            string reason)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            ProcessId = processId;
            PreviousState = previousState;
            NewState = newState;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
        }

        /// <summary>
        /// Adds context information to the state change event
        /// </summary>
        /// <param name="key">The context key</param>
        /// <param name="value">The context value</param>
        public void AddContext(string key, object value)
        {
            Context[key] = value;
        }

        /// <summary>
        /// Returns a string representation of the session state change event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"SessionStateChanged: Session={SessionId}, Process={ProcessId}, " +
                   $"{PreviousState} -> {NewState}, Reason={Reason}";
        }
    }

    /// <summary>
    /// Event arguments for consensus detection events
    /// </summary>
    public class ConsensusDetectionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the session identifier
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// Gets the process identifier
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Gets the consensus decision
        /// </summary>
        public ConsensusDecision Decision { get; }

        /// <summary>
        /// Gets the consensus confidence level
        /// </summary>
        public double Confidence { get; }

        /// <summary>
        /// Gets the number of detectors that participated in the consensus
        /// </summary>
        public int DetectorCount { get; }

        /// <summary>
        /// Gets the number of detectors that agreed with the decision
        /// </summary>
        public int AgreementCount { get; }

        /// <summary>
        /// Gets the individual detector results
        /// </summary>
        public List<DetectorResult> DetectorResults { get; }

        /// <summary>
        /// Gets the consensus timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the consensus method used
        /// </summary>
        public ConsensusMethod ConsensusMethod { get; }

        /// <summary>
        /// Initializes a new instance of the ConsensusDetectionEventArgs class
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <param name="processId">The process identifier</param>
        /// <param name="decision">The consensus decision</param>
        /// <param name="confidence">The confidence level</param>
        /// <param name="detectorCount">The number of detectors</param>
        /// <param name="agreementCount">The number of agreeing detectors</param>
        /// <param name="detectorResults">The individual detector results</param>
        /// <param name="consensusMethod">The consensus method used</param>
        public ConsensusDetectionEventArgs(
            string sessionId,
            int processId,
            ConsensusDecision decision,
            double confidence,
            int detectorCount,
            int agreementCount,
            List<DetectorResult> detectorResults,
            ConsensusMethod consensusMethod)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            ProcessId = processId;
            Decision = decision;
            Confidence = confidence;
            DetectorCount = detectorCount;
            AgreementCount = agreementCount;
            DetectorResults = detectorResults ?? throw new ArgumentNullException(nameof(detectorResults));
            Timestamp = DateTime.UtcNow;
            ConsensusMethod = consensusMethod;
        }

        /// <summary>
        /// Returns a string representation of the consensus detection event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"ConsensusDetection: Session={SessionId}, Process={ProcessId}, " +
                   $"Decision={Decision}, Confidence={Confidence:F2}, " +
                   $"Agreement={AgreementCount}/{DetectorCount}, Method={ConsensusMethod}";
        }
    }

    /// <summary>
    /// Defines the possible session states
    /// </summary>
    public enum SessionState
    {
        /// <summary>
        /// Session is active and being used
        /// </summary>
        Active,

        /// <summary>
        /// Session is idle but may become active again
        /// </summary>
        Idle,

        /// <summary>
        /// Session has been idle for a significant time
        /// </summary>
        LongIdle,

        /// <summary>
        /// Session is considered inactive and may be released
        /// </summary>
        Inactive,

        /// <summary>
        /// Session state is unknown or undetermined
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Defines the consensus decision types
    /// </summary>
    public enum ConsensusDecision
    {
        /// <summary>
        /// Consensus indicates session is active
        /// </summary>
        Active,

        /// <summary>
        /// Consensus indicates session is idle
        /// </summary>
        Idle,

        /// <summary>
        /// Consensus is inconclusive
        /// </summary>
        Inconclusive,

        /// <summary>
        /// Consensus indicates session should be released
        /// </summary>
        Release
    }

    /// <summary>
    /// Defines the consensus methods
    /// </summary>
    public enum ConsensusMethod
    {
        /// <summary>
        /// Simple majority vote
        /// </summary>
        Majority,

        /// <summary>
        /// Weighted voting based on detector confidence
        /// </summary>
        Weighted,

        /// <summary>
        /// Unanimous agreement required
        /// </summary>
        Unanimous,

        /// <summary>
        /// Threshold-based agreement
        /// </summary>
        Threshold,

        /// <summary>
        /// Hierarchical decision making
        /// </summary>
        Hierarchical
    }

    /// <summary>
    /// Represents a detector result for consensus evaluation
    /// </summary>
    public class DetectorResult
    {
        /// <summary>
        /// Gets the detector name
        /// </summary>
        public string DetectorName { get; set; }

        /// <summary>
        /// Gets the detection result
        /// </summary>
        public bool IsIdle { get; set; }

        /// <summary>
        /// Gets the confidence level
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets the idle time detected
        /// </summary>
        public TimeSpan IdleTime { get; set; }

        /// <summary>
        /// Gets the detection method
        /// </summary>
        public string DetectionMethod { get; set; }

        /// <summary>
        /// Gets additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Initializes a new instance of the DetectorResult class
        /// </summary>
        public DetectorResult()
        {
            Metadata = new Dictionary<string, object>();
        }

        /// <summary>
        /// Returns a string representation of the detector result
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"DetectorResult: {DetectorName}, IsIdle={IsIdle}, " +
                   $"Confidence={Confidence:F2}, IdleTime={IdleTime.TotalMinutes:F1}m";
        }
    }
}