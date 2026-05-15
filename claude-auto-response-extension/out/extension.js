"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
let isEnabled = true;
let statusBarItem;
let outputChannel;
// Patterns that indicate a permission prompt from Claude Code
const PERMISSION_PROMPT_PATTERNS = [
    // Standard permission prompts
    /\[Y\]es\s*\/\s*\[N\]o/i,
    /\[1\]\s*Yes.*\[2\]\s*No/i,
    /Press 1 for Yes/i,
    /Allow\?/i,
    /Do you want to (allow|proceed|continue)/i,
    /Permission required/i,
    // Claude Code specific patterns (adjust based on actual output)
    /Allow Claude to/i,
    /Claude wants to/i,
];
// Buffer to accumulate terminal output (prompts may come in chunks)
const terminalBuffers = new Map();
const BUFFER_TIMEOUT = 500; // ms to wait for complete prompt
const bufferTimers = new Map();
function activate(context) {
    outputChannel = vscode.window.createOutputChannel('Claude Auto-Response');
    log('Extension activated');
    // Create status bar item
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBarItem.command = 'claudeAutoResponse.status';
    updateStatusBar();
    statusBarItem.show();
    context.subscriptions.push(statusBarItem);
    // Load saved state
    isEnabled = vscode.workspace.getConfiguration('claudeAutoResponse').get('enabled', true);
    // Register commands
    context.subscriptions.push(vscode.commands.registerCommand('claudeAutoResponse.enable', () => {
        isEnabled = true;
        vscode.workspace.getConfiguration('claudeAutoResponse').update('enabled', true, true);
        updateStatusBar();
        vscode.window.showInformationMessage('Claude Auto-Response: Enabled');
        log('Enabled by user');
    }));
    context.subscriptions.push(vscode.commands.registerCommand('claudeAutoResponse.disable', () => {
        isEnabled = false;
        vscode.workspace.getConfiguration('claudeAutoResponse').update('enabled', false, true);
        updateStatusBar();
        vscode.window.showInformationMessage('Claude Auto-Response: Disabled');
        log('Disabled by user');
    }));
    context.subscriptions.push(vscode.commands.registerCommand('claudeAutoResponse.status', () => {
        const status = isEnabled ? 'ENABLED' : 'DISABLED';
        vscode.window.showInformationMessage(`Claude Auto-Response is ${status}`);
    }));
    // Listen for terminal output (PROPOSED API - requires VS Code Insiders)
    try {
        // @ts-ignore - This is a proposed API
        const disposable = vscode.window.onDidWriteTerminalData((event) => {
            if (!isEnabled)
                return;
            const terminal = event.terminal;
            const data = event.data;
            log(`Terminal "${terminal.name}" output: ${data.substring(0, 100)}${data.length > 100 ? '...' : ''}`);
            // Accumulate data in buffer
            const currentBuffer = terminalBuffers.get(terminal) || '';
            terminalBuffers.set(terminal, currentBuffer + data);
            // Clear existing timer
            const existingTimer = bufferTimers.get(terminal);
            if (existingTimer) {
                clearTimeout(existingTimer);
            }
            // Set new timer to process buffer
            const timer = setTimeout(() => {
                processTerminalBuffer(terminal);
            }, BUFFER_TIMEOUT);
            bufferTimers.set(terminal, timer);
        });
        context.subscriptions.push(disposable);
        log('Successfully registered onDidWriteTerminalData listener');
    }
    catch (error) {
        log(`ERROR: Failed to register terminal listener: ${error}`);
        vscode.window.showErrorMessage('Claude Auto-Response: Failed to access terminal API. ' +
            'Make sure you are running VS Code Insiders with --enable-proposed-api flag.');
    }
    // Clean up when terminals are closed
    context.subscriptions.push(vscode.window.onDidCloseTerminal((terminal) => {
        terminalBuffers.delete(terminal);
        const timer = bufferTimers.get(terminal);
        if (timer) {
            clearTimeout(timer);
            bufferTimers.delete(terminal);
        }
        log(`Terminal "${terminal.name}" closed, cleaned up buffers`);
    }));
}
function processTerminalBuffer(terminal) {
    const buffer = terminalBuffers.get(terminal) || '';
    terminalBuffers.set(terminal, ''); // Clear buffer
    if (buffer.trim() === '')
        return;
    log(`Processing buffer for "${terminal.name}": ${buffer.substring(0, 200)}`);
    // Check if this looks like a permission prompt
    if (isPermissionPrompt(buffer)) {
        log(`DETECTED permission prompt in "${terminal.name}"`);
        sendResponse(terminal);
    }
}
function isPermissionPrompt(text) {
    // Remove ANSI escape codes for cleaner matching
    const cleanText = text.replace(/\x1b\[[0-9;]*m/g, '');
    for (const pattern of PERMISSION_PROMPT_PATTERNS) {
        if (pattern.test(cleanText)) {
            log(`Matched pattern: ${pattern}`);
            return true;
        }
    }
    return false;
}
function sendResponse(terminal) {
    const delay = vscode.workspace.getConfiguration('claudeAutoResponse').get('responseDelay', 100);
    setTimeout(() => {
        // Send '1' for Yes (Claude Code uses numbered options)
        terminal.sendText('1', false);
        log(`Sent '1' to terminal "${terminal.name}"`);
        // Also try sending Enter after a brief delay in case it's needed
        setTimeout(() => {
            terminal.sendText('', true); // Empty string with addNewLine=true sends Enter
            log(`Sent Enter to terminal "${terminal.name}"`);
        }, 50);
        vscode.window.setStatusBarMessage('Claude Auto-Response: Approved prompt', 2000);
    }, delay);
}
function updateStatusBar() {
    if (isEnabled) {
        statusBarItem.text = '$(check) Claude AR';
        statusBarItem.tooltip = 'Claude Auto-Response: Enabled (click for status)';
        statusBarItem.backgroundColor = undefined;
    }
    else {
        statusBarItem.text = '$(x) Claude AR';
        statusBarItem.tooltip = 'Claude Auto-Response: Disabled (click for status)';
        statusBarItem.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
    }
}
function log(message) {
    const timestamp = new Date().toISOString();
    outputChannel.appendLine(`[${timestamp}] ${message}`);
}
function deactivate() {
    log('Extension deactivated');
}
//# sourceMappingURL=extension.js.map