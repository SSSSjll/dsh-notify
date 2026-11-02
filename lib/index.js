import { spawn } from 'node:child_process'
import { existsSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs'
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
    state.child = spawn(EXE, [], { stdio: 'ignore', windowsHide: true })
    state.child.on('error', () => { state.child = null })
    state.child.on('exit', () => { state.child = null })
  } catch { state.child = null }
}

function stopProcess(state) {
  try { if (state.child) state.child.kill() } catch {}
  state.child = null
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