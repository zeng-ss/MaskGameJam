// Lua 语法批量校验（luaparse）
//
// 用法（在项目根目录执行）：
//   NODE_PATH="C:/Users/ASUS/.workbuddy/binaries/node/workspace/node_modules" \
//   "C:/Users/ASUS/.workbuddy/binaries/node/versions/22.22.2-6/node.exe" Tools/lua_check.js
//
// 首次使用需要先装依赖：
//   cd C:/Users/ASUS/.workbuddy/binaries/node/workspace && npm install luaparse
//
// 作用：改完 Lua 之后先跑一遍，能在打开 Unity 之前就发现语法错误（少一次编译等待）。
const fs = require('fs');
const path = require('path');

let luaparse;
try {
    luaparse = require('luaparse');
} catch (e) {
    console.error('缺少 luaparse 依赖，请先执行：');
    console.error('  cd C:/Users/ASUS/.workbuddy/binaries/node/workspace && npm install luaparse');
    process.exit(2);
}

function walk(dir, out) {
    if (!fs.existsSync(dir)) return;
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const p = path.join(dir, entry.name);
        if (entry.isDirectory()) walk(p, out);
        else if (entry.name.endsWith('.lua.txt')) out.push(p);
    }
}

const files = [];
walk('Assets/LuaScripts', files);

let fail = 0;
for (const f of files) {
    const src = fs.readFileSync(f, 'utf8');
    try {
        luaparse.parse(src, { luaVersion: '5.3' });
        console.log('OK   ' + f);
    } catch (e) {
        fail++;
        console.log('FAIL ' + f + '  ->  ' + e.message);
    }
}

console.log('---');
console.log('共 ' + files.length + ' 个文件' + (fail ? ('，失败 ' + fail + ' 个') : '，全部语法通过'));
process.exit(fail ? 1 : 0);
