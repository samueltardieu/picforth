fwanted = 2000
duty = 0.5
tosc = 1.0/26000000
m = 1.0
for i in range (0,3):
	for pr2 in range (1,256):
		prescaler = 1 << (2*i)
		cycle = (pr2+1)*4*tosc*prescaler
		B = 1 / cycle
		d = abs (B - fwanted) / fwanted
		if d < m:
			ccpr = duty*cycle/(tosc*prescaler)
			m = d
			print "presc=%d, pr2=%d, err=%.3f%%, freq=%.3fHz, ccpr=%d (%d,%d)" \
				% (prescaler, pr2, d*100, B, ccpr, ccpr/4, ccpr%4)
