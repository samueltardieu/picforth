#! /usr/bin/env python

"""
PIC Hex File downloader utility using the PicForth monitor

written Oct 2004 by David McNab
"""

# serial device for talking to bootmon
defaultDev = "/dev/ttyS0"

# how fast we're talking - uncomment one
#defaultBaudrate = 9600
#defaultBaudrate = 19200
#defaultBaudrate = 28800
#defaultBaudrate = 33600
defaultBaudrate = 57600

# size of program memory
defaultImageSize = 8192
#defaultImageSize = 4096

import sys, os, termios, tty, time, getopt

class BadConnection(Exception):
    """Failed to talk to picforth bootloader"""

class InvalidAddress(Exception):
    """Trying to read from invalid address on PIC"""

progname = sys.argv[0]

quiet = False

defaultOptions = {
    'port' : defaultDev,
    'speed' : defaultBaudrate,
    'cmd' : None,
    'path' : None,
    'size' : defaultImageSize,
    }

class Hexfile:
    """
    parses an Intel hex file
    """
    def __init__(self, **options):
    
        f = options['path']
    
        if isinstance(f, str):
            base, ext = os.path.splitext(f)
            if ext == '':
                f += ".hex"
            f = file(f)
    
        if not hasattr(f, 'read'):
            err("Invalid file object %s" % repr(f))
            usage()
    
        self.rawrecs = {}
    
        for line in f.readlines():       # grab each line from the file
            self.parseLine(line)         # decompose into address:data
    
        # write in the program start vector
        #org = options['entry']
        #vector = options['vector']
        #orgH = org >> 8
        #self.rawrecs[vector] = 0x3000 | orgH  # movlw HIGH(progOrg)
        #self.rawrecs[vector+1] = 0x8a         # movwf PCLATH
        #self.rawrecs[vector+2] = 0x2800 | org # goto progOrg
        #log("Added app entry point 0x%x to vector at 0x%x\n" % (org, vector))
    
        # fill in any 'blanks' - make sure that for every entry, there's
        # actually a block of 4 words (needed for 16F87xA)
        for addr in self.rawrecs.keys():
            # nuke out b0 and b1
            addr1 = (addr >> 2) << 2
            
            for i in range(0, 4):
                if not self.rawrecs.has_key(addr1+i):
                    #print "Adding empty word for addr 0x%x" % (addr1+i)
                    self.rawrecs[addr1+i] = 0
    
        # sort the keys
        self.addresses = self.rawrecs.keys()
        self.addresses.sort()
        
        # and create list of address,value pairs
        self.data = [(addr, self.rawrecs[addr]) for addr in self.addresses]
    
        # and convert this to lines with ASCII addresses and data, ready for download
        self.ascdata = [(("%04X" % addr), ("%04X" % val)) for (addr, val) in self.data]
    
    def __getitem__(self, item):
        return self.data[item]
    
    def __len__(self):
        return len(self.data)
        
    def parseLine(self, line):
        """
        parses an individual line of Intel Hex file
        """
        hexlen = line[1:3]        # number of data bytes on the line
        hexaddr = line[3:7]       # starting address of data on line
        hextype = line[7:9]       # 00 - data, 01 - eof
        hexdata = line[9:-2]      # data bytes (use words for the 14 bit PIC instructions)
        #hexcrc = line[-2:]        # crc of line
    
        linelen = self.fromHex(hexlen)    # decompose line length
        lineaddr = self.fromHex(hexaddr)  # decompose starting address
    
        if hextype != '01':                 # if not eof
            for i in range(0, linelen, 2):    # grab each data word
                s = hexdata[i*2+2:i*2+4] + hexdata[i*2:i*2+2]
                val = self.fromHex(s)
                #print "%s = %s" % (s, val)
                #self[(lineaddr + i)/2] = hexdata[i*2:i*2+4]    # populate dict. with addr:data
                self.rawrecs[(lineaddr + i)/2] = val
    
    def fromHex(self, s):
        
        return int(s, 16)
    
    def keys(self):
        """
        Convenience wrapper - returns dict keys in order
        """
        keys = dict.keys(self)
        keys.sort()
        return keys
    
    def hexkeys(self):
        """
        For debugging, returns keys in order, as hex
        """
        return "[" + ", ".join([("%x" % k) for k in self.keys()]) + "]"
    
class Tty:
    """
    Wraps a serial link to PIC
    """
    def __init__(self, **options):
        
        try:
            speedToken = getattr(termios, "B%s" % options['speed'])
        except:
            raise Exception("Invalid baudrate %s" % repr(options['speed']))
    
        fd = os.open(options['port'], os.O_RDWR | os.O_NONBLOCK, 0777)
        attr = termios.tcgetattr(fd)
        attr[4] = attr[5] = speedToken
        termios.tcsetattr(
            fd,
            termios.TCSANOW,
            attr)
        tty.setraw(fd)
    
        self.fd = fd
    
    def read(self, n=1, blocking=False):
        while 1:
            try:
                c = os.read(self.fd, n)
            except OSError:
                c = ''
            if c or not blocking:
                return c
    
        return None  # satisfy pychecker
    
    def write(self, s):
        os.write(self.fd, s)
    
    def __del__(self):
        os.close(self.fd)
    
class PIC:
    """
    Abstract class for interfacing with a PIC chip via local serial
    link connected to PIC, talking to PicForth serial monitor
    """
    def __init__(self, **kw):
        """
        Connects to PicForth serial monitor
        
        Arguments:
            dev - device to use for talking to PIC, default is defaultDev
            callback - a callback which will be invoked while trying to connect
        """
        options = dict(defaultOptions)
        options.update(kw)
    
        self.options = options
    
        self.tty = Tty(**options)
        
    def connect(self, callback=None):
        """
        Repeatedly sends an invalid character (space) to PicForth monitor,
        and looks for '?>' response. Barfs if any other chars are received,
        which indicates incorrect serial parameters (eg baudrate)
        """
        attempts = 0
        while True:
            try:
                self.tty.write(" ")
                c = self.tty.read()
                if c == '':
                    if callback:
                        callback()
                        time.sleep(0.1)
                    continue
        
                if c not in ['?', '>']:
                    raise BadConnection("Invalid reply char: check line settings")
        
                #print "Connecting, bootmon sent %s" % repr(c)
        
                # connected, now drain rx
                while 1:
                    time.sleep(0.1)
                    c = self.tty.read()
                    if c == '':
                        break
                    if c not in ['?', '>']:
                        raise BadConnection("Invalid reply char: check line settings")
                    #print "Connecting, bootmon sent %s" % repr(c)
                break
            except BadConnection:
                attempts += 1
                if attempts % 100 == 0:
                    attempts = 0
                    callback(True)
    
    def download(self, hexfile=sys.stdin, **kw):
        """
        Downloads a file
        """
        options = dict(defaultOptions)
        options.update(kw)
        options['path'] = hexfile
    
        # create Hexfile object if not already one
        if not isinstance(hexfile, Hexfile):
            hexfile = Hexfile(**options)
    
        # send down a series of 'F' commands
        i = 0
    
        # ask serial monitor where we should write our start vector
        vector = self.getMonVector()
        print "Serial monitor wants boot vector written to 0x%x" % vector
        
        # compute values for determining address legality
        vecRomStart = "%04X" % (vector - 4)
        vecRomEnd = "%04X" % (vector - 1)
        #vecAsciiStart = "%04X" % vector
        vecAsciiEnd = "%04X" % (vector + 3)
        cfgStart = "2000"
        cfgEnd = "2100"
        eeStart = "2100"
        eeEnd = "2200"
    
        for addr, val in hexfile.ascdata:
            # relocate addresses 0-3 to vector instead
            if addr < "0004":
                addrOld = addr
                addr = "%04X" % (int(addr, 16) + vector)
                print "Relocated %s from %s to %s" % (val, addrOld, addr)
            
            # intercept eeprom addresses
            if addr >= eeStart and addr < eeEnd:
                #print "Want to set eeprom %s = %s" % (addr, val)
                addr = addr[2:4]
                val = val[2:4]
                self.emem_write(addr, val)
                continue
    
            # vet address
            #if (addr < "0004") or (addr >= "0E40" and addr <= "0FFF"):
            if (addr < "0004") \
                    or (addr >= cfgStart and addr <= cfgEnd) \
                    or (addr >= vecRomStart and addr <= vecRomEnd) \
                    or (addr > vecAsciiEnd and addr < eeEnd):
                #print "Skipping invalid setting %s->%s" % (addr, val)
                continue
            else:
                self.pmem_write(addr, val)
    
                # display user feedback
                i += 1
                if i % 16 == 0:
                    if i % 256 == 0:
                        if i % 1024 == 0:
                            log("%dk\n" % (i / 1024))
                        else:
                            log("*")
                    else:
                        log(".")
    
                    sys.stdout.flush()
    
        if i % 1024:
            log('\n')
    
        log("Successfully transferred %d bytes\n" % i)
    
        log("Download successful\n")
    
    def getMonVector(self):
        """
        Asks serial monitor where we should place our vector
        """
        self.tty.write("o")
    
        # expect ack
        c = self.tty.read(blocking=1)
        if c != '!':
            raise Exception("Wanted '!', got %s" % repr(c))
    
        chars = []
        for i in range(4):
            chars.append(self.tty.read(blocking=1))
        addr = int("".join(chars), 16)
        
        # expect prompt
        c = self.tty.read(blocking=1)
        if c != '>':
            raise Exception("Wanted '>', got %s" % repr(c))
    
        return addr
    
    def upload(self, hexfile=sys.stdout, **kw):
        """
        Uploads image from device, outputs in Intel hex format
        """
        image = {}
        imageSize = kw['size']
        
        # suck 8k of program mem, plus flash
        for i in (range(0, imageSize) + range(0x2000, 0x2100)):
        #for i in range(0x100):
            image[i] = self[i]
    
            if (i+1) % 16 == 0:
                if (i+1) % 256 == 0:
                    if (i+1) % 1024 == 0:
                        log("%dk\n" % ((i+1)/1024))
                    else:
                        log("*")
                else:
                    log(".")
        log("\n")
    
        # create hexfile records - need to write as little-endian words
        hexlines = []
        addresses = image.keys()
        addresses.sort()
        for addr in (range(0, imageSize, 8) + range(0x2000, 0x2100, 8)):
        #for addr in range(0, 0x100, 8):
    
            bytes = []           # raw bytes to write
            haddr = addr * 2     # .hex file addresses are double, since we're writing words
    
            # write out length
            bytes.append(16)
    
            # write out address field
            bytes.append(haddr / 0x100)
            bytes.append(haddr % 0x100)
    
            # write out record type
            bytes.append(0)
    
            # write out data bytes
            for j in range(addr, addr+8):
                val = image[j]
                #log("%x=%x\n" % (j, val))
                bytes.append(val % 0x100)   # lsb
                bytes.append(val / 0x100)   # msb
    
            # compute and write out checksum
            cksm = (0x100 - (sum(bytes) % 0x100)) % 0x100
            bytes.append(cksm)
            
            # now can output the record
            hexlines.append(":" + "".join([("%02X" % b) for b in bytes]))
    
        # now spit out the 'special line'
        hexlines.append(":00000001FF")
    
        # assemble into hex file image
        heximage = "\n".join(hexlines) + "\n"
    
        # and dump it
        if isinstance(hexfile, str):
            base, ext = os.path.splitext(hexfile)
            if ext == '':
                hexfile += ".hex"
            hexfile = file(hexfile, "w")
        hexfile.write(heximage)
    
    def pmem_write(self, addr, val):
        """
        Writes a word to program memory
        """
        cmd = "F%s%s" % (addr, val)
        #print cmd
        self.tty.write(cmd)
    
        # seek reply chars from bootmon
        for c in ['!', '>']:
            c1 = self.tty.read(blocking=1)
            if c1 != c:
                print "Bootmon replied with %s, wanted '!'" % repr(c1)
                raise Exception("Got reply %s, wanted %s" % (
                    repr(c1), repr(c)))
    
    def pmem_read(self, addr):
        """
        Reads program memory at chosen location
        """
        # issue read command
        cmd = "f%04X" % addr
        self.tty.write(cmd)
        
        # read back ack/nack
        c = self.tty.read(blocking=1)
        if c != '!':
            raise Exception("Bad response from program memory read")
        
        # ack'ed ok, read nybbles
        val = 0
        for i in range(4):
            c = self.tty.read(blocking=1).upper()
            if c not in "0123456789ABCDEF":
                raise Exception("Bad value from program memory read")
            if c.isdigit():
                nyb = ord(c) - ord('0')
            else:
                nyb = ord(c) - ord('A') + 10
            val = val << 4 | nyb
    
        # expect final '>'
        c = self.tty.read(blocking=1)
        if c != '>':
            raise Exception("Invalid final response from program memory read")
    
        return val
    
    def emem_read(self, addr):
        """
        Reads data eeprom memory at chosen location
        """
        if addr > 0xff:
            raise Exception("Invalid eeprom data address 0x%x" % addr)
    
        # issue read command
        cmd = "e%02X" % addr
        self.tty.write(cmd)
        
        # read back ack/nack
        c = self.tty.read(blocking=1)
        if c != '!':
            raise Exception("Bad response from program memory read")
        
        # ack'ed ok, read nybbles
        val = 0
        for i in range(2):
            c = self.tty.read(blocking=1).upper()
            if c not in "0123456789ABCDEF":
                raise Exception("Bad value from program memory read")
            if c.isdigit():
                nyb = ord(c) - ord('0')
            else:
                nyb = ord(c) - ord('A') + 10
            val = val << 4 | nyb
    
        # expect final '>'
        c = self.tty.read(blocking=1)
        if c != '>':
            raise Exception("Invalid final response from program memory read")
    
        return val
    
    def emem_write(self, addr, val):
        """
        Writes a word to data eeprom memory
        """
        cmd = "E%02X%02X" % (int(addr, 16), int(val, 16))
        #print cmd
        self.tty.write(cmd)
    
        # seek reply chars from bootmon
        for c in ['!', '>']:
            c1 = self.tty.read(blocking=1)
            if c1 != c:
                print "Bootmon replied with %s, wanted '!'" % repr(c1)
                raise Exception("Got reply %s, wanted %s" % (
                    repr(c1), repr(c)))
    
    def run_firmware(self):
        """
        Issues 'O' command to launch firmware
        """
        #self.tty.write("X0005")
        self.tty.write("O")
    
    def __getitem__(self, addr):
        if addr < 0x2000:
            return self.pmem_read(addr)
        elif addr >= 0x2000 and addr < 0x2100:
            return self.emem_read(addr - 0x2000)
    
        else:
            raise InvalidAddress("Attempted to read from invalid address 0x%x" % addr)
    
def do_download(**options):
    """
    Attempts to download an intel hex file to a PIC over serial
    """
    # create a PIC interface object
    pic = PIC(**options)

    # callback routine for connecting
    def conn_callback(err=False):
        if err:
            log("?")
        else:
            log(".")

    # get a connection
    log("Connecting to PicForth monitor (press Ctrl-C to abort):")
    try:
        pic.connect(conn_callback)
    except KeyboardInterrupt:
        log("\nDownload aborted by user")
        return

    log("\nConnected sucessfully, now downloading:\n")
    
    # do the download
    pic.download(options['path'], **options)

    # now send command to run prog
    log("Launching application\n")
    pic.run_firmware()

def do_upload(**options):
    """
    rips the image from a pic, outputs to hex file
    """
    # create a PIC interface object
    pic = PIC(**options)

    # callback routine for connecting
    def conn_callback():
        log(".")

    # get a connection
    log("Connecting to PicForth monitor (press Ctrl-C to abort):")
    try:
        pic.connect(conn_callback)
    except KeyboardInterrupt:
        log("\nUpload aborted by user")
        return

    log("\nConnected sucessfully, now uploading:\n")
    
    # do the download
    pic.upload(options['path'], **options)

def usage(ret=1):

    err("Usage: %s [options] [filename]\n" % progname)
    err("PicForth monitor interaction utility\n")
    err("Options:\n")
    err("   -q, --quiet\n")
    err("       Suppresses output (except errors)\n")
    err("   -p, --port=/dev/ttywhatever\n")
    err("       Selects serial port device (default %s)\n" % defaultDev)
    err("   -s, --speed=nnnn\n")
    err("       Sets line speed (default %s)\n" % defaultBaudrate)
    err("   -d, --download\n")
    err("       Downloads application to device\n")
    err("   -u, --upload\n")
    err("       Uploads memory image from device\n")
    err("   -4, --4k\n")
    err("       Limits uploads to 4k (for 16f88, 16f873 etc\n")
    err("   -2, --2k\n")
    err("       Limits uploads to 2k (for PICs with only 2k\n")
    err("   -1, --1k\n")
    err("       Limits uploads to 1k (for 16f84 and other 1k procs\n")
    err("One of -u,-d should be specified, if not, '-d' is assumed\n")
    err("If filename arg is not given, uses stdin or stdout\n")
    err("\n")
    err("Examples:\n")
    err("   cat myprog.hex | ./pfd.py -p /dev/ttyS2 -s 19200\n")
    err("      - downloads myprog.hex via /dev/ttyS2 at 19k2\n")
    err("   ./pfd.py fred.hex\n")
    err("      - downloads fred.hex using defaults (%s at %s)\n" % (defaultDev, defaultBaurate))

    sys.exit(ret)

def err(s):
    sys.stderr.write(s)
    sys.stderr.flush()

def main():

    global quiet

    options = dict(defaultOptions)

    # extract cmd line args
    try:
        opts, args = getopt.getopt(
            sys.argv[1:],
            "?hqp:s:421du",
            ["help", "quiet", "port=", "speed=",
             "4k", "2k", "1k",
             "download", "upload",
             ])
    except getopt.GetoptError:
        # print help information and exit:
        usage()
        sys.exit(2)

    # process options
    for o, a in opts:
        # help request
        if o in ("-h", "-?", "--help"):
            usage(0)

        # gag output messages
        elif o in ("-q", "--quiet"):
            quiet = True

        # choose serial port device
        elif o in ("-p", "--port"):
            options['port'] = a

        # choose line speed
        elif o in ("-s", "--speed"):
            options['speed'] = a

        # Set size-limiting options
        elif o in ("-4", "--4k"):
            options['size'] = 4096
        elif o in ("-2", "--2k"):
            options['size'] = 2048
        elif o in ("-1", "--1k"):
            options['size'] = 1024

        # choose download mode
        elif o in ("-d", "--download"):
            if options['cmd'] == 'upload':
                err("Upload and Download are mutually exclusive!\n")
                usage()
            options['cmd'] = "download"

        # choose upload mode
        elif o in ("-u", "--upload"):
            if options['cmd'] == 'download':
                err("Upload and Download are mutually exclusive!\n")
                usage()
            options['cmd'] = "upload"

    # grab filename arg, if any
    if len(args) > 0:
        # barf if more than one arg
        if len(args) > 1:
            err("Too many arguments\n")
            usage(1)
        options['path'] = args[0]

    # barf if no command given
    if not options['cmd']:
        print "No command, defaulting to download"
        options['cmd'] = "download"

    # do download, if asked to    
    if options['cmd'] == 'download':
        if not options['path']:
            options['path'] = sys.stdin
        do_download(**options)

    # do upload, if asked to
    elif options['cmd'] == 'upload':
        if not options['path']:
            options['path'] = sys.stdout
        do_upload(**options)

def log(s):
    if not quiet:
        err(s)

if __name__ == '__main__':
    main()
