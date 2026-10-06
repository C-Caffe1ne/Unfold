from pathlib import Path
import json, math, statistics, sys
run=Path(sys.argv[1])
rows=[json.loads(l) for l in (run/'samples.jsonl').read_text().splitlines() if l.strip()]
result=json.loads((run/'result.json').read_text())
# The final row is appended after elapsed.Stop() and the end capture.
# Keep it in raw evidence and use its time to close the observation interval,
# but exclude it from resource summaries and plots.
periodic=rows[:-1]
warm=120
steady=[r for r in periodic if r['elapsedSeconds']>=warm]
def describe(xs):
 xs=sorted(xs)
 return {'min':min(xs),'median':statistics.median(xs),'p95':xs[round((len(xs)-1)*.95)],'max':max(xs)}
def median_window(start,end,key):
 xs=[r[key] for r in periodic if start<=r['elapsedSeconds']<end]
 return statistics.median(xs) if xs else None
def slope(key):
 xs=[r['elapsedSeconds']/60 for r in steady];ys=[r[key]/2**20 for r in steady]
 xm=statistics.mean(xs);ym=statistics.mean(ys)
 den=sum((x-xm)**2 for x in xs)
 return sum((x-xm)*(y-ym) for x,y in zip(xs,ys))/den if den else None
times={'sampledGlbSeconds':0.,'sampledVisibleGlbSeconds':0.,'sampled2dSeconds':0.}
for a,b in zip(rows,rows[1:]):
 dt=b['elapsedSeconds']-a['elapsedSeconds'];u=a.get('ui') or {}
 times['sampledGlbSeconds' if u.get('Glb') else 'sampled2dSeconds']+=dt
 if u.get('Glb') and u.get('Visible'):times['sampledVisibleGlbSeconds']+=dt
final=rows[-1]['elapsedSeconds']
out={'samples':len(rows),'lastSeconds':final,'warmupSeconds':warm,'steadySamples':len(steady),
 'excludedTerminationSample':{'elapsedSeconds':rows[-1]['elapsedSeconds'],
  'reason':'Final append occurs after the measurement stopwatch stops and the end capture. Its CPU numerator can include capture work; exclude it from resource summaries and plots.'},
 'excludedInitialCpuPlotSample':'First CPU delta spans setup-to-first-append, not the normal sampling interval; omit it from the CPU plot. Warmup already excludes it from summary statistics.',
 'rssMiB':describe([r['rssBytes']/2**20 for r in steady]),
 'managedMiB':describe([r['managedBytes']/2**20 for r in steady]),
 'lastGcHeapMiB':describe([r['heapAfterLastGcBytes']/2**20 for r in steady]),
 'cpuOneCorePercent':describe([r['cpuOneCorePercent'] for r in steady]),
 'cpuMachinePercent':describe([r['cpuMachinePercent'] for r in steady]),
 'rssEarly5minMedianMiB':median_window(warm,warm+300,'rssBytes')/2**20,
 'rssLast5minMedianMiB':median_window(max(warm,final-300),final+1,'rssBytes')/2**20,
 'managedEarly5minMedianMiB':median_window(warm,warm+300,'managedBytes')/2**20,
 'managedLast5minMedianMiB':median_window(max(warm,final-300),final+1,'managedBytes')/2**20,
 'rssSlopeMiBPerMinute':slope('rssBytes'),'managedSlopeMiBPerMinute':slope('managedBytes'),
 'maxHeartbeatAgeSeconds':max(r['uiHeartbeatAgeSeconds'] for r in steady),
 'endPlaybackFailures':rows[-1]['playbackFailures'],
 'observedTimeEstimates':times,
 'timeEstimateCaveat':'5-second endpoint samples; transient model switch/hide dwell is approximate, not exact per-frame duration',
 'comparison':'single current native run; no matched old native run, so no improvement/no-regression claim'}
imports=json.loads((run/'imports.json').read_text())
names={m['installedId']:Path(m['Detail']['file']).stem for m in imports}
def summarize_group(group):
 return {'samples':len(group),
  'rssMiB':describe([r['rssBytes']/2**20 for r in group]),
  'managedMiB':describe([r['managedBytes']/2**20 for r in group]),
  'cpuOneCorePercent':describe([r['cpuOneCorePercent'] for r in group])}
out['byCharacter']={}
out['byCharacterAction']={}
for character in sorted({r['ui']['Character'] for r in steady}):
 group=[r for r in steady if r['ui']['Character']==character]
 label=names.get(character,character)
 out['byCharacter'][label]=summarize_group(group)
 out['byCharacterAction'][label]={action:summarize_group([r for r in group if r['ui']['Action']==action])
  for action in sorted({r['ui']['Action'] for r in group})}
out['groupCaveat']='UI label at sample endpoint; CPU covers the preceding interval and may cross a transition. Shared-process memory includes prior workload and is not isolated model cost.'
(run/'analysis.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(out,ensure_ascii=False,indent=2))
try:
 import matplotlib
 matplotlib.use('Agg')
 import matplotlib.pyplot as plt
 fig,axes=plt.subplots(3,1,figsize=(10,8),sharex=True)
 x=[r['elapsedSeconds']/60 for r in periodic]
 axes[0].plot(x,[r['rssBytes']/2**20 for r in periodic],label='RSS')
 axes[0].plot(x,[r['managedBytes']/2**20 for r in periodic],label='Managed (no forced GC)',alpha=.75)
 axes[0].set_ylabel('MiB');axes[0].legend(loc='upper right')
 axes[1].plot(x[1:],[r['cpuOneCorePercent'] for r in periodic[1:]],color='#725BAA',linewidth=1)
 axes[1].set_ylabel('CPU %\n100 = one core')
 axes[2].plot(x,[r['uiHeartbeatAgeSeconds'] for r in periodic],color='#3C8060',linewidth=1)
 axes[2].set_ylabel('UI heartbeat\nage (seconds)');axes[2].set_xlabel('Elapsed minutes')
 for ax in axes:
  ax.axvspan(0,warm/60,color='gray',alpha=.12);ax.grid(alpha=.2)
 fig.suptitle('Unfold 1.1.0-beta native macOS GLB soak — current build only')
 fig.tight_layout();fig.savefig(run/'resource-trend.png',dpi=160);plt.close(fig)
except ModuleNotFoundError:
 print('Plot unavailable; JSON remains saved.')
