'use strict';
// File & folder icon emoji map
const FILE_ICONS = {
  // Folders
  'folder':           '📁',
  'folder-desktop':   '🖥',
  'folder-docs':      '📄',
  'folder-downloads': '⬇',
  'folder-pics':      '🖼',
  'folder-music':     '🎵',
  'folder-video':     '🎬',
  'folder-git':       '🔀',
  'folder-node':      '📦',
  'folder-build':     '⚙',
  // Drives
  'harddisk':         '💽',
  'usb':              '🔌',
  'cdrom':            '💿',
  'network':          '🌐',
  // Documents
  'pdf':              '📕',
  'word':             '📘',
  'excel':            '📗',
  'ppt':              '📙',
  // Archives
  'archive':          '📦',
  // Media
  'image':            '🖼',
  'video':            '🎬',
  'audio':            '🎵',
  // Code
  'csharp':           '🔷',
  'js':               '🟨',
  'ts':               '🔷',
  'python':           '🐍',
  'java':             '☕',
  'cpp':              '⚙',
  'c':                '©',
  'header':           '📎',
  'html':             '🌐',
  'css':              '🎨',
  'json':             '📋',
  'xml':              '📋',
  'sql':              '🗄',
  'shell':            '⚫',
  'bat':              '⚫',
  'powershell':       '🔵',
  // System
  'exe':              '▶',
  'dll':              '🔩',
  'disk':             '💿',
  'font':             '🔤',
  // Text
  'text':             '📄',
  'file':             '📄',
};

function getFileIcon(iconKey) {
  return FILE_ICONS[iconKey] || '📄';
}
