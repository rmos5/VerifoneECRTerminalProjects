
# Verifone ECR Terminal Projects

This Visual Studio solution provides a complete implementation of the **Verifone Electronic Cash Register (ECR) communication interface** for .NET.  
It includes a reusable class library for terminal communication and a WPF sample application that demonstrates and validates the ECR protocol behavior.

## Solution Overview

**Solution file:** `VerifoneECRTerminalProjects.sln`  
**Target environment:** .NET Framework 4.6.2  
**Developed with:** Visual Studio 2022 (solution format version 12.00)

### 1. Verifone.ECRTerminal (Class Library)

This project contains the complete implementation of the **ECR communication protocol** for Verifone payment terminals.  
It exposes a high-level API for controlling terminal operations, handling transactions, and processing responses.

**Main responsibilities:**
- Manage communication between the POS system and the Verifone payment terminal.
- Provide event-driven notification for all protocol responses.
- Implement all essential transaction types (payment, refund, reversal, abort, retrieval).
- Control display messages, bonus and auxiliary modes.
- Maintain internal state control to prevent overlapping operations.
- Offer session tracking and structured exception handling.
- Support integration as a standalone NuGet package.

**Typical use cases:**
- Integrate Verifone terminal operations into POS or ERP systems.
- Implement transaction automation or testing utilities.
- Provide a consistent and fault-tolerant interface for ECR transactions.

**Structure overview:**
```
Verifone.ECRTerminal/
├─ \n├─ Verifone.ECRTerminal.csproj.dtbcache.json\n├─ \n├─ AssemblyInfo.cs\n├─ CommonStrings.fi-FI.txt\n├─ CommonStrings.txt\n├─ CustomerBonusStatus.fi-FI.txt\n├─ CustomerBonusStatus.txt\n├─ Strings.Designer.cs\n├─ Strings.resx\n├─ TransactionStatusPhase.fi-FI.txt\n├─ TransactionStatusPhase.txt\n├─ TransactionStatusResultCode.fi-FI.txt\n├─ TransactionStatusResultCode.txt\n├─ TransactionStatusResultCodeUserPrompt.fi-Fi.txt\n├─ TransactionStatusResultCodeUserPrompt.txt\n├─ \n├─ 
```

### 2. VerifonePaymentTerminal (WPF Application)

This project serves as a **diagnostic and demonstration application** for testing and validating the ECR library.

**Main responsibilities:**
- Provide a user interface for interacting with the Verifone terminal.
- Allow configuration of COM port and operational settings.
- Execute test transactions, display terminal output, and show protocol events.
- Offer an environment for developers to verify integration behavior.

**App features:**
- Manual and automated transaction initiation.
- Visual monitoring of responses and events.
- Configurable connection parameters (port, timeout, tracing).
- UI options for handling bonus card operations and display text modes.

**Structure overview:**
```
VerifonePaymentTerminal/
├─ \n├─ AssemblyInfo.cs\n├─ Settings.Designer.cs\n├─ Settings.settings\n├─ \n├─ \n├─ 
```

### 3. Shared Components and Packaging

- **NuGet Packaging:**  
  The library includes a `.nuspec` file for generating a NuGet package (`Verifone.ECRTerminal.nuspec`).
- **Configuration:**  
  Application-level settings are defined in `App.config` for port and behavior customization.
- **Documentation Support:**  
  The solution includes metadata suitable for DocFX documentation generation.

### Summary

| Project | Type | Purpose |
|----------|------|----------|
| Verifone.ECRTerminal | Class Library | Core ECR protocol implementation |
| VerifonePaymentTerminal | WPF Application | Diagnostic and demonstration tool |

This structure ensures the separation of concerns:  
the **library** is suitable for production use and integration in other systems,  
while the **WPF app** provides an interactive way to test, validate, and demonstrate the protocol logic.

_Last updated: 2025-10-28_
