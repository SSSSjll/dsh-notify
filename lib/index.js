import { spawn } from 'node:child_process'
import { existsSync, readFileSync, writeFileSync, mkdirSync, watch, statSync, openSync, readSync, closeSync, readdirSync } from 'node:fs'
import * as zlib from 'node:zlib'
import { homedir } from 'node:os'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

export const name = 'dsh-notify'
export const inject = ['webServer']

const PKG = join(dirname(fileURLToPath(import.meta.url)), '..')
const EXE = join(PKG, 'assets', 'dsh-notify.exe')
const DATA_DIR = join(homedir(), '.dsh', 'dsh-notify')
const INI = join(DATA_DIR, 'config.ini')

const DEFAULTS = {
  enabled: '1',
  title: '\u2714  \u8bf7\u6c42\u5df2\u5b8c\u6210',
  sub: '\u70b9\u8fd9\u91cc\u56de\u5230 DeepSeek Harness',
  width: '336',
  height: '124',
  anim_in: '800',
  anim_out: '420',
  hold_ms: '30000',
  pos: 'br',
  margin_x: '0',
  margin_y: '10',
  image: '',
  image_size: '56',
  font: 'Microsoft YaHei UI',
  title_size: '11.5',
  sub_size: '9',
  align: 'left',
  img_pos: 'left',
  exit_fx: 'auto',
  fx_ms: '800',
  text_x: '-1',
  text_y: '-1',
}

function ensureIni() {
  try {
    if (existsSync(INI)) return
    mkdirSync(DATA_DIR, { recursive: true })
    const body = Object.entries(DEFAULTS).map(([k, v]) => k + '=' + v).join('\n') + '\n'
    writeFileSync(INI, body, 'utf8')
  } catch {}
}

function readCfg() {
  const out = { ...DEFAULTS }
  try {
    if (!existsSync(INI)) return out
    for (const raw of readFileSync(INI, 'utf8').split(/\r?\n/)) {
      const line = raw.trim()
      if (!line || line.startsWith('#')) continue
      const i = line.indexOf('=')
      if (i <= 0) continue
      out[line.slice(0, i).trim().toLowerCase()] = line.slice(i + 1).trim()
    }
  } catch {}
  return out
}

function writeCfg(patch) {
  const cfg = { ...readCfg(), ...patch }
  const body = Object.entries(cfg).map(([k, v]) => k + '=' + v).join('\n') + '\n'
  try { mkdirSync(DATA_DIR, { recursive: true }); writeFileSync(INI, body, 'utf8') } catch {}
  return cfg
}

function startProcess(state) {
  if (state.child || !existsSync(EXE)) return
  const cfg = readCfg()
  if (cfg.enabled === '0') return
  try {
    state.child = spawn(EXE, ['--no-watch'], { stdio: 'ignore', windowsHide: true })
    state.child.on('error', () => { state.child = null })
    state.child.on('exit', () => { state.child = null })
  } catch { state.child = null }
}

function stopProcess(state) {
  try { if (state.child) state.child.kill() } catch {}
  state.child = null
}

const FRAME_MAGIC = Buffer.from([0x28, 0xb5, 0x2f, 0xfd])

function newestSession() {
  const root = join(homedir(), '.dsh', 'sessions')
  let best = null
  const walk = (dir) => {
    let entries
    try { entries = readdirSync(dir, { withFileTypes: true }) } catch { return }
    for (const e of entries) {
      const p = join(dir, e.name)
      if (e.isDirectory()) { walk(p); continue }
      if (!e.name.endsWith('.zstd')) continue
      try {
        const m = statSync(p).mtimeMs
        if (!best || m > best.mtime) best = { path: p, mtime: m }
      } catch {}
    }
  }
  walk(root)
  return best ? best.path : null
}

// 解压一段（可能含多个 zstd 帧），返回 { text, complete }
function decodeFrames(buf) {
  const zs = zlib.zstdDecompressSync
  if (typeof zs !== 'function') return { text: null, complete: buf.length }
  const starts = []
  let i = buf.indexOf(FRAME_MAGIC, 0)
  while (i >= 0) { starts.push(i); i = buf.indexOf(FRAME_MAGIC, i + 4) }
  if (!starts.length) return { text: null, complete: 0 }
  const parts = []
  let complete = 0
  for (let k = 0; k < starts.length; k++) {
    const s = starts[k]
    const e = k + 1 < starts.length ? starts[k + 1] : buf.length
    try {
      parts.push(zs(buf.subarray(s, e)))
      complete = e
    } catch {
      // 末尾半帧：留到下次
      break
    }
  }
  return { text: Buffer.concat(parts).toString('utf8'), complete }
}

const ASK_TOOLS = new Set(['ask_user_question', 'request_approval', 'ask', 'approval_request'])

function startTurnWatch(show) {
  const root = join(homedir(), '.dsh', 'sessions')
  if (!existsSync(root)) return () => {}
  let file = newestSession()
  let offset = file ? statSync(file).size : 0
  let timer = null
  let stopped = false
  const canDecode = typeof zlib.zstdDecompressSync === 'function'
  let quiet = null

  const tick = () => {
    if (stopped) return
    const f = newestSession()
    if (!f) return
    if (f !== file) {
      file = f
      try { offset = statSync(f).size } catch { offset = 0 }
      return
    }
    let size = 0
    try { size = statSync(f).size } catch { return }
    if (size <= offset) return
    const len = size - offset
    const buf = Buffer.alloc(len)
    try {
      const fd = openSync(f, 'r')
      readSync(fd, buf, 0, len, offset)
      closeSync(fd)
    } catch { return }

    const { text, complete } = decodeFrames(buf)
    offset += complete || len
    if (text == null) return
    let last = null
    for (const line of text.split('\n')) {
      const t = line.trim()
      if (!t) continue
      try { last = JSON.parse(t) } catch {}
    }
    if (!last) return
    const kind = last.type
    const toolName = (last.data && last.data.name) || ''
    if (kind === 'turn/end') show('done')
    else if (kind === 'tool/call' && ASK_TOOLS.has(toolName)) show('ask')
  }

  let deb = null
  const schedule = () => {
    if (!canDecode) {
      // 运行时不支持 zstd：退回到「安静 6 秒」的启发式
      if (quiet) clearTimeout(quiet)
      quiet = setTimeout(() => show('done'), 6000)
      if (quiet.unref) quiet.unref()
      return
    }
    if (deb) clearTimeout(deb)
    deb = setTimeout(tick, 700)
    if (deb.unref) deb.unref()
  }
  try { watch(root, { recursive: true }, schedule) } catch {}
  return () => { stopped = true; if (deb) clearTimeout(deb) }
}

function sendJson(res, status, obj) {
  const body = JSON.stringify(obj)
  res.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' })
  res.end(body)
}

function readRaw(req) {
  return new Promise((resolve, reject) => {
    const chunks = []
    let size = 0
    req.on('data', (c) => {
      size += c.length
      if (size > 8 * 1024 * 1024) { reject(new Error('too large')); return }
      chunks.push(c)
    })
    req.on('end', () => resolve(Buffer.concat(chunks)))
    req.on('error', reject)
  })
}

function readBody(req) {
  return readRaw(req).then((b) => b.toString('utf8')).catch(() => '')
}

export function apply(ctx) {
  const state = { child: null }

  ctx.effect(() => {
    ensureIni()
    startProcess(state)
    const show = (kind) => {
      try {
        if (!existsSync(EXE)) return
        const args = kind === 'ask' ? ['--show', '--ask'] : ['--show']
        spawn(EXE, args, { stdio: 'ignore', windowsHide: true })
      } catch {}
    }
    const stopWatch = startTurnWatch(show)
    return () => { stopWatch(); stopProcess(state) }
  }, 'dsh-notify: process')

  ctx.effect(
    () =>
      ctx.webServer.register({
        kind: 'prefix',
        path: '/dsh-notify',
        handler: async (req, res) => {
          const url = (req.url || '/').split('?')[0]
          try {
            if (url === '/dsh-notify/config' && (req.method || 'GET') === 'GET') {
              return sendJson(res, 200, { ok: true, config: readCfg(), running: state.child !== null })
            }
            if (url === '/dsh-notify/config' && req.method === 'POST') {
              const patch = JSON.parse((await readBody(req)) || '{}')
              const cfg = writeCfg(patch)
              if (cfg.enabled === '0') stopProcess(state)
              else { stopProcess(state); startProcess(state) }
              return sendJson(res, 200, { ok: true, config: cfg, running: state.child !== null })
            }
            // \u4e0a\u4f20\u63d2\u56fe\uff1a\u539f\u59cb\u4e8c\u8fdb\u5236\uff0c?name=xxx.png
            if (url === '/dsh-notify/image' && req.method === 'POST') {
              const buf = await readRaw(req)
              if (!buf.length) return sendJson(res, 400, { ok: false, error: 'empty' })
              const q = (req.url || '').split('?')[1] || ''
              const m = /(?:^|&)name=([^&]*)/.exec(q)
              const raw = m ? decodeURIComponent(m[1]) : 'image.png'
              const ext = ((raw.split('.').pop() || 'png').toLowerCase().replace(/[^a-z0-9]/g, '')) || 'png'
              mkdirSync(DATA_DIR, { recursive: true })
              const target = join(DATA_DIR, 'image.' + ext)
              writeFileSync(target, buf)
              const cfg = writeCfg({ image: target })
              return sendJson(res, 200, { ok: true, path: target, config: cfg })
            }
            if (url === '/dsh-notify/image/clear' && req.method === 'POST') {
              return sendJson(res, 200, { ok: true, config: writeCfg({ image: '' }) })
            }
            if (url === '/dsh-notify/test' && req.method === 'POST') {
              let ok = false
              try {
                if (existsSync(EXE)) {
                  spawn(EXE, ['--test'], { stdio: 'ignore', windowsHide: true })
                  ok = true
                }
              } catch {}
              return sendJson(res, 200, { ok })
            }
            return sendJson(res, 404, { ok: false, error: 'not found' })
          } catch (e) {
            return sendJson(res, 500, { ok: false, error: e instanceof Error ? e.message : String(e) })
          }
        },
      }),
    'dsh-notify: http route',
  )
}