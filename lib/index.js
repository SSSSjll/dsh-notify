import { spawn } from 'node:child_process'
import { existsSync, readFileSync, writeFileSync, mkdirSync, rmSync } from 'node:fs'
import { homedir } from 'node:os'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

export const name = 'dsh-notify'
export const inject = ['webServer']

const DIR = 'C:\\Users\\Lenovo\\Desktop\\deepseek\\02-DSH\u63d0\u9192'
const EXE = join(DIR, 'dsh-notify.exe')
const INI = join(DIR, 'config.ini')

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
}

function ensureIni() {
  try {
    if (existsSync(INI)) return
    mkdirSync(join(homedir(), '.dsh', 'dsh-notify'), { recursive: true })
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
  writeFileSync(INI, body, 'utf8')
  return cfg
}

function startProcess(state) {
  if (state.child || !existsSync(EXE)) return
  const cfg = readCfg()
  if (cfg.enabled === '0') return
  try {
    state.child = spawn(EXE, [], { stdio: 'ignore', windowsHide: true })
    state.child.on('error', () => { state.child = null })
    state.child.on('exit', () => { state.child = null })
  } catch { state.child = null }
}

function stopProcess(state) {
  try { if (state.child) state.child.kill() } catch {}
  state.child = null
}

function probe() {
  const dir = join(homedir(), '.dsh', 'sessions', '__dsh_notify_probe__')
  try {
    mkdirSync(dir, { recursive: true })
    writeFileSync(join(dir, 'ping.zstd'), String(Date.now()))
    setTimeout(() => { try { rmSync(dir, { recursive: true, force: true }) } catch {} }, 8000)
    return true
  } catch { return false }
}

function sendJson(res, status, obj) {
  const body = JSON.stringify(obj)
  res.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' })
  res.end(body)
}

function readBody(req) {
  return new Promise((resolve) => {
    const chunks = []
    req.on('data', (c) => chunks.push(c))
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')))
    req.on('error', () => resolve(''))
  })
}

export function apply(ctx) {
  const state = { child: null }

  ctx.effect(() => {
    startProcess(state)
    return () => stopProcess(state)
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
              const body = await readBody(req)
              const patch = JSON.parse(body || '{}')
              const cfg = writeCfg(patch)
              if (cfg.enabled === '0') stopProcess(state)
              else { stopProcess(state); startProcess(state) }
              return sendJson(res, 200, { ok: true, config: cfg, running: state.child !== null })
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
