import csv, json, pathlib, statistics, sys

directory = pathlib.Path(sys.argv[1])
rows = list(csv.DictReader((directory / 'samples.csv').open()))
run = json.loads((directory / 'run.json').read_text())
if not rows:
    failure = {'budget_pass': False, 'run': run, 'validation_errors': ['No memory samples were collected.']}
    (directory/'summary.json').write_text(json.dumps(failure, indent=2))
    print(json.dumps(failure, indent=2))
    raise SystemExit(1)
columns = ['rss_bytes', 'physical_footprint_bytes', 'managed_bytes', 'heap_last_gc_bytes', 'committed_last_gc_bytes']
columns += [key for key in ['graphics_footprint_bytes', 'graphics_footprint_compressed_bytes',
            'purgeable_nonvolatile_bytes', 'purgeable_nonvolatile_compressed_bytes',
            'device_bytes', 'device_peak_bytes', 'reusable_bytes', 'external_bytes',
            'graphics_nofootprint_bytes', 'graphics_nofootprint_compressed_bytes'] if key in rows[0]]
groups = {}
for row in rows:
    groups.setdefault(row['phase'], []).append(row)
def summary(values):
    result = {}
    for key in columns:
        items = sorted(int(r[key]) for r in values)
        result[key] = {'min': items[0], 'mean': statistics.mean(items), 'p50': items[len(items)//2], 'p95': items[min(len(items)-1, int(len(items)*.95))], 'max': items[-1], 'last': int(values[-1][key])}
    result['samples'] = len(values)
    result['seconds'] = (float(values[-1]['elapsed_ms']) - float(values[0]['elapsed_ms']))/1000
    result['allocated_delta_bytes'] = int(values[-1]['allocated_bytes']) - int(values[0]['allocated_bytes'])
    result['collections'] = {g: int(values[-1][g])-int(values[0][g]) for g in ['gen0','gen1','gen2']}
    result['budget_pass'] = result['rss_bytes']['max'] < 400_000_000 and result['physical_footprint_bytes']['max'] < 400_000_000
    if 'cpu_total_ms' in values[0] and result['seconds'] > 0:
        result['cpu_delta_seconds'] = (float(values[-1]['cpu_total_ms']) - float(values[0]['cpu_total_ms']))/1000
        result['cpu_mean_percent_one_core'] = result['cpu_delta_seconds'] / result['seconds'] * 100
    return result
report = {'sample_interval_ms': 200, 'budget_bytes': 400_000_000, 'run': run,
          'overall': summary(rows), 'lifetime_rss_peak_bytes': max(int(r['rss_peak_bytes']) for r in rows),
          'lifetime_physical_peak_bytes': max(int(r['physical_peak_bytes']) for r in rows),
          'mach_errors': sorted(set(int(r['mach_error']) for r in rows)),
          'phases': {key: summary(values) for key,values in groups.items()},
          'statistical_boundary': 'Time-series samples are correlated. Percentiles describe the observed run; they do not establish a population confidence interval or an absolute limit for arbitrary inputs.'}
report['validation_errors'] = []
if not any(event.get('phase') == 'finished' for event in run.get('events', [])):
    report['validation_errors'].append('The workload did not record its finished event.')
if report['lifetime_rss_peak_bytes'] <= 0 or report['lifetime_physical_peak_bytes'] <= 0:
    report['validation_errors'].append('Native lifetime peaks must both be positive.')
report['budget_pass'] = report['overall']['budget_pass'] and report['lifetime_rss_peak_bytes'] < 400_000_000 and report['lifetime_physical_peak_bytes'] < 400_000_000 and report['mach_errors'] == [0] and report['run']['success'] and not report['validation_errors']
minute_groups = {}
for row in rows:
    minute_groups.setdefault(int(float(row['elapsed_ms']) // 60000), []).append(row)
report['minute_trend'] = {str(minute): summary(values) for minute,values in minute_groups.items()}
soak = [row for row in rows if row['phase'].startswith('soak-cycle-')]
if soak:
    report['soak'] = summary(soak)
    start = float(soak[0]['elapsed_ms']); points = []
    for minute,values in minute_groups.items():
        if any(row['phase'].startswith('soak-cycle-') for row in values):
            points.append(((statistics.mean(float(row['elapsed_ms']) for row in values)-start)/60000,
                           statistics.mean(int(row['physical_footprint_bytes']) for row in values)))
    if len(points) >= 3:
        xs, ys = zip(*points); xmean = statistics.mean(xs); ymean = statistics.mean(ys)
        slope = sum((x-xmean)*(y-ymean) for x,y in points) / sum((x-xmean)**2 for x in xs)
        report['soak']['physical_trend_bytes_per_minute'] = slope
        report['soak']['physical_first_minute_mean_bytes'] = ys[0]
        report['soak']['physical_last_minute_mean_bytes'] = ys[-1]
        report['soak']['trend_boundary'] = 'Descriptive least-squares slope of minute means; repeated model/scale changes confound leak inference and observations are autocorrelated.'
(directory/'summary.json').write_text(json.dumps(report, indent=2))
print(json.dumps({key: report[key] for key in ['budget_pass','overall','lifetime_rss_peak_bytes','lifetime_physical_peak_bytes','mach_errors']},indent=2))
