#
# Makefile for PicForth -- don't hesitate to get ideas from it as it
# automates the process of building files for PIC quite well
#

COMPILER=	picforth.fs
LIBRARIES=	picisr.fs libfetch.fs libstore.fs picflash.fs piceeprom.fs \
		librshift.fs liblshift.fs multitasker.fs priotasker.fs \
		libnibble.fs libcmove.fs libstrings.fs

PROGS=		booster.hex generator.hex silver.hex \
		spifcard.hex taskexample.hex controller.hex i2cloader.hex \
		libroll.hex libfetch.hex libjtable.hex libstore.hex \
		librshift.hex liblshift.hex libnibble.hex libcmove.hex \
		libstrings.hex libextra.hex

GFORTH?=	gforth-0.6.2

DISASM=		${PROGS:.hex=.disasm}

all:	${DISASM} CHANGES.html docs
docs:
	cd doc && ${MAKE} RELEASEVERSION=${RELEASEVERSION}
.PHONY:	all docs

.SUFFIXES: .fs .hex .asm .disasm .dump .serprog

interactive:
	${GFORTH} picforth.fs

.fs.hex: ${COMPILER} ${LIBRARIES}
	${GFORTH} picforth.fs -e 'include $< file-dump ${<:.fs=.hex} \
		write-map ${<:.fs=.map} bye'

.fs.disasm: ${COMPILER} ${LIBRARIES}
	${GFORTH} picforth.fs -e 'include $< file-dump ${<:.fs=.hex} \
		write-map ${<:.fs=.map} write-dis ${<:.fs=.disasm} bye'

.hex.asm:
	gpdasm $< > $@

.disasm.dump:
	less $<

.fs.serprog: ${COMPILER} ${LIBRARIES} serial.fs
	${MAKE} ${<:fs=hex}
	${GFORTH} picforth.fs -e 'include $< include serial.fs serprog bye'
#	${GFORTH} picforth.fs -e 'include $< include serial.fs serprog firmware bye'

RELEASEVERSION = 1.2.4
DEVELOPMENTBRANCH = picforth-1

release:
	${MAKE} all
	mkdir picforth-${RELEASEVERSION}
	tar cf - `cat MANIFEST` | (cd picforth-${RELEASEVERSION} && tar xvf -)
	chmod -R og=u-w picforth-${RELEASEVERSION}
	tar zcf picforth-${RELEASEVERSION}.tar.gz picforth-${RELEASEVERSION}
	rm -rf picforth-${RELEASEVERSION}
	chmod a+r picforth-${RELEASEVERSION}.tar.gz

CHANGES.html: CHANGES support/makedoc.pl
	perl support/makedoc.pl < CHANGES > CHANGES.html

WWWDIR=/home/sam/build/rfc1149--zengarden/devel

installwww:
	${MAKE} release
	${MAKE} installwww2

installwww2:
	cp booster.fs booster.disasm ${WWWDIR}
	cp CHANGES.html ${WWWDIR}/CHANGES.picforth.html
	cp doc/picforth.html doc/picforth.pdf ${WWWDIR}/doc
	scp -p picforth-${RELEASEVERSION}.tar.gz \
		www.rfc1149.net:rfc1149.net/data/download/picforth/
	echo "Do not forget to edit ${WWWDIR}/picforth.whtml"

taskexample.hex: multitasker.fs
taskexample.disasm: multitasker.fs

clean::
	rm -rf *.hex *.map *.disasm CHANGES.html *~ \
		tests/*.hex tests/*.map tests/*.disasm testresults
	cd doc && ${MAKE} clean

test::
	${MAKE} release all
	env MAKE=${MAKE} sh support/runtests.sh
	cp ${PROGS} ${DISASM} testresults/
	diff --unified --recursive tests/expected testresults
	rm -rf testresults

mirror::
	rm -rf mirror-dist
	darcs get . mirror-dist
	chmod -R og=u-w mirror-dist
	rsync -av --delete mirror-dist/ www.rfc1149.net:rfc1149.net/data/download/picforth-repository/${DEVELOPMENTBRANCH}/
	rm -rf mirror-dist

update-tests::
	$(MAKE) clean
	grep -v '^tests/' MANIFEST > MANIFEST.tmp
	find tests -name '*.fs' -o -name '*.disasm' -o -name '*.hex' >> MANIFEST.tmp
	mv MANIFEST.tmp MANIFEST
