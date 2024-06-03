# Seawatcher3000

## This gist of it

This project idea is an "automatic seawatcher", which involves motorised, computer-controlled zooming, panning, and tilting of a DSLR camera, and the Nikon SDK for receiving live images, focusing, and triggering the shutter. The aim is to use an object detection model trained on images of seabirds to detect a bird (the camera is set up pointed at the sea), zoom onto and track it, and take a photo. 

## Technical notes

Zooming seems doable with the right engineering, but the panning and tilting will probably require 3D printing. There are camera gimbals with the functionality but they're commercial products and there's probably no way for me to control them with my own computer program. 

The object detection model only needs to register that there is a bird in the photo and point out its location so it can be zoomed in on and focused on. I do not expect it to classify species at such a distance, although it could be interesting to see how it fairs in that regard. I'll probably use my own images for training as I've done plenty of seawatching in my free time (and even when I should be doing other things). I assume I'm gonna end up having to draw lots of bounding boxes?

C# wrapper for Nikon SDK will be used to interact with the camera. YOLOv8 on Python for object detection.

## Connecting C# and Python

Going to try with a local socket server:
    - Both the C# program and Python program running simultaneaously.
    - C# streams video to the server which the Python program reads.
    - Python outputs to server the coordinates and size (and possibly direction if we need to be a second ahead) of birds, when applicable. 
    - C# responds to the information on the server. 
    - NOTE: We still may run the Python from C# as a simulated command line script, but just to get it up and running without manually starting two programs. I/O will be through the server. 
