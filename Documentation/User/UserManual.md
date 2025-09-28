# License Release Service - User Manual

## Overview

Welcome to the License Release Service User Manual. This guide explains how the Idle Detection system works and how it affects your SolidWorks license usage. The system automatically detects when you're not actively using SolidWorks and releases your license back to the pool for other users.

## What is Idle Detection?

The Idle Detection system is an intelligent feature that monitors your SolidWorks usage to determine when you're not actively working. When the system detects that you've been idle for a specified period, it automatically releases your license back to the license pool.

### Benefits for Users

- **Fair License Distribution**: Ensures licenses are available when you need them
- **Automatic License Management**: No manual intervention required
- **Transparent Operation**: Works in the background without disrupting your work
- **Improved Resource Utilization**: Helps the organization optimize license costs

## How It Works

### Detection Methods

The system uses multiple methods to determine activity:

1. **Time-Based Detection**: Tracks time since your last interaction
2. **System Activity Monitoring**: Monitors keyboard, mouse, and system activity
3. **Application-Specific Monitoring**: Tracks SolidWorks-specific activities
4. **File System Monitoring**: Monitors SolidWorks document access and modifications

### Detection Thresholds

The system uses graduated thresholds:

| Threshold | Time | Action |
|-----------|------|--------|
| Warning | 5 minutes | System prepares for potential release |
| Imminent | 10 minutes | System alerts that license may be released soon |
| Critical | 15 minutes | System releases license if no activity detected |

*Note: Thresholds may vary based on your organization's configuration.*

### Detection Process

```
Start → Activity Detected → Reset Timer
↓
No Activity → Warning Threshold → User Notified
↓
No Activity → Imminent Threshold → Final Warning
↓
No Activity → Critical Threshold → License Released
```

## User Experience

### Normal Operation

During normal operation, you won't notice the system running. It monitors your activity in the background and only takes action when you've been idle for an extended period.

### Activity Detection

The system considers you active if you:

- **Keyboard Activity**: Type on your keyboard
- **Mouse Activity**: Move or click your mouse
- **SolidWorks Activity**: Work on SolidWorks documents
- **Window Activity**: Have SolidWorks windows in focus
- **File Activity**: Open, save, or modify SolidWorks files

### Notifications

When the system detects inactivity, you may receive notifications:

1. **Warning Notification**: After 5 minutes of inactivity
2. **Imminent Notification**: After 10 minutes of inactivity
3. **Release Notification**: When license is released (15 minutes)

### License Recovery

If your license is released while you're still working:

1. **Automatic Recovery**: The system attempts to reacquire a license when you resume activity
2. **Seamless Experience**: Your work is saved and you can continue working
3. **Grace Period**: You have a brief period to save your work before license release

## Best Practices

### To Maintain License Access

- **Regular Activity**: Perform small actions if you're thinking or reviewing
- **Save Frequently**: Save your work regularly (this counts as activity)
- **Use Break Reminders**: Set reminders to move your mouse during long breaks
- **Stay in Application**: Keep SolidWorks open and visible when working

### During Breaks

- **Short Breaks** (<5 minutes): No action needed
- **Medium Breaks** (5-10 minutes): Consider saving your work
- **Long Breaks** (>15 minutes): Save work and let license release naturally

### When Returning from Breaks

- **Immediate Action**: Move your mouse or press a key
- **Check Application**: SolidWorks will automatically attempt to reacquire a license
- **Resume Work**: Continue working normally once license is reacquired

## Troubleshooting Common Issues

### License Released While Working

**Symptoms**: License released despite being active
**Solutions**:
1. **Check Activity**: Ensure you're actively interacting with SolidWorks
2. **Verify Settings**: Confirm your system isn't in sleep/hibernate mode
3. **Check Network**: Ensure network connectivity to license server
4. **Contact Support**: If issue persists, contact your system administrator

### Slow License Reacquisition

**Symptoms**: Takes time to get license back after returning from break
**Solutions**:
1. **Wait Patiently**: Allow time for automatic recovery
2. **Manual Intervention**: Try manually saving or reopening files
3. **Check Availability**: Verify licenses are available in the pool
4. **Restart Application**: As a last resort, restart SolidWorks

### Notification Issues

**Symptoms**: Not receiving idle warnings
**Solutions**:
1. **Check Settings**: Verify notifications are enabled in your system
2. **Check Firewall**: Ensure notifications aren't blocked
3. **Update Application**: Ensure you're using the latest version
4. **Contact IT**: Check if notification service is running

## FAQ

### General Questions

**Q: Why was my license released?**
A: Your license was released because the system detected no activity for the configured threshold period (typically 15 minutes).

**Q: How do I prevent my license from being released?**
A: Stay active in SolidWorks by performing regular actions like saving files, moving your mouse, or typing.

**Q: Will I lose my work if my license is released?**
A: No, your work is not lost. When you return, the system will attempt to reacquire a license and you can continue working.

**Q: Can I configure my own idle detection settings?**
A: Individual user settings are typically managed by your system administrator to ensure fair license distribution.

**Q: What happens if there are no licenses available when I return?**
A: You'll be placed in a queue and will receive a license as soon as one becomes available.

### Technical Questions

**Q: Does the system monitor my personal activities?**
A: No, the system only monitors SolidWorks-related activities and basic system interactions. It does not monitor personal data or activities outside of SolidWorks.

**Q: Does the system work offline?**
A: The system requires network connectivity to communicate with the license server, but basic detection functions work offline.

**Q: Can I override the automatic release?**
A: In most configurations, automatic release cannot be overridden to ensure fair license distribution. Contact your administrator for specific policies.

**Q: Does the system work with all versions of SolidWorks?**
A: The system is designed to work with multiple SolidWorks versions. Contact your administrator for version-specific compatibility.

**Q: How much system resources does the monitoring use?**
A: The monitoring is designed to be lightweight and typically uses less than 5% CPU and minimal memory.

## Privacy and Security

### Data Collection

The system collects only the following information:
- Process names and IDs for SolidWorks applications
- Basic system activity (keyboard, mouse, window focus)
- File access patterns for SolidWorks documents
- License usage statistics

### Privacy Protection

- **No Personal Data**: Personal files and activities outside SolidWorks are not monitored
- **Anonymized Data**: Usage statistics are aggregated and anonymized
- **Secure Storage**: All data is stored securely and encrypted
- **Audit Trail**: All license operations are logged for compliance

### Security Measures

- **Authentication**: Only authorized users can access licenses
- **Encryption**: All communications are encrypted
- **Access Control**: Strict access controls prevent unauthorized access
- **Audit Logging**: All activities are logged for security review

## Integration with Your Workflow

### Daily Use

1. **Start Work**: Open SolidWorks and begin working normally
2. **Work Sessions**: The system monitors your activity automatically
3. **Take Breaks**: Take breaks without worrying about license management
4. **Return to Work**: Resume working - license recovery is automatic
5. **End of Day**: Close SolidWorks when finished for the day

### Meeting Scenarios

**Before Meeting**:
- Save your work
- Leave SolidWorks open
- System will handle license management during meeting

**During Meeting**:
- No action required
- License may be released if meeting exceeds threshold

**After Meeting**:
- Return to workstation
- Move mouse or press key to reactivate
- Resume working when license is reacquired

### Lunch Breaks

- Save your work before leaving
- System will automatically release license during extended break
- License will be reacquired when you return

## Support and Resources

### Getting Help

**Internal Resources**:
- IT Help Desk: Extension 5555
- Email: support@company.com
- Intranet: http://helpdesk.company.com

**Self-Service Resources**:
- Knowledge Base: http://kb.company.com/solidworks
- Video Tutorials: http://training.company.com
- FAQ Section: http://faq.company.com/licenses

### Reporting Issues

When reporting issues, please provide:
- Your username and computer name
- Date and time of the issue
- What you were doing when the issue occurred
- Any error messages received
- Steps you've already taken to resolve the issue

### Training Resources

**Available Training**:
- **Basic Usage**: 30-minute overview session
- **Advanced Features**: 1-hour deep dive
- **Administrator Training**: 2-hour technical session
- **Troubleshooting**: 1-hour problem-solving workshop

**Schedule**:
- Weekly sessions available
- On-demand training for teams
- Online modules available 24/7
- Custom training available upon request

## Glossary

**Term** | **Definition**
---|---
**Idle Detection** | The process of determining when a user is not actively using an application
**License Pool** | The shared pool of licenses available to all users
**Threshold** | The time period after which a license may be released
**Consensus Engine** | The component that combines multiple detection methods
**Hysteresis** | The period that prevents rapid state changes
**Activity Monitoring** | The process of tracking user interactions
**License Release** | The act of returning a license to the pool
**License Reacquisition** | The process of obtaining a license from the pool
**Grace Period** | A brief period before license release takes effect

## Version Information

**Current Version**: 2.0.0
**Release Date**: September 2024
**Supported SolidWorks Versions**: 2018-2024
**System Requirements**: Windows 10/11, .NET Framework 4.8

## Contact Information

**Primary Support**: helpdesk@company.com
**Emergency Support**: emergency@company.com
**Feedback**: feedback@company.com
**Documentation**: docs.company.com/licenses

---

*This user manual is subject to change as features are added and improved. Always check the company intranet for the most up-to-date documentation.*